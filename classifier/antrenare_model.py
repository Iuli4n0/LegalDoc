import pandas as pd
import re
import csv
from safetensors.torch import load_file, save_file
import numpy as np
from pathlib import Path
from datasets import Dataset
from sklearn.model_selection import GroupShuffleSplit
from transformers import (
    AutoTokenizer,
    AutoModelForSequenceClassification,
    TrainingArguments,
    Trainer,
    EarlyStoppingCallback,
)
from sklearn.metrics import accuracy_score, precision_recall_fscore_support
import argparse
import shutil

SEED = 42
MAX_LENGTH = 512


def fix_and_save_layernorm(model, save_path: str) -> "AutoModelForSequenceClassification":
    """Rename LayerNorm keys (gamma→weight, beta→bias) and save to disk.

    The dumitrescustefan checkpoint uses the old naming convention (gamma/beta).
    Simply renaming in RAM is not enough: when Trainer saves a checkpoint with
    load_best_model_at_end=True, it overwrites the on-disk file with the old keys,
    causing LayerNorm layers to be randomly re-initialized on reload.

    rename keys, save to disk, reload — Trainer will then persist
    correct keys in every subsequent checkpoint.
    """
    state_dict = model.state_dict()
    new_state_dict = {}
    renamed = 0
    for key, value in state_dict.items():
        new_key = key
        if "LayerNorm.gamma" in key:
            new_key = key.replace("LayerNorm.gamma", "LayerNorm.weight")
            renamed += 1
        elif "LayerNorm.beta" in key:
            new_key = key.replace("LayerNorm.beta", "LayerNorm.bias")
            renamed += 1
        new_state_dict[new_key] = value

    if renamed > 0:
        print(f"Fix LayerNorm: {renamed} keys renamed (gamma→weight, beta→bias).")
        # Apply the renamed state_dict in RAM before saving
        model.load_state_dict(new_state_dict, strict=False)
        # Save to disk with correct keys
        model.save_pretrained(save_path)
        # Reload from disk — Trainer will now save correctly at each checkpoint
        model = AutoModelForSequenceClassification.from_pretrained(
            save_path,
            num_labels=model.config.num_labels,
            ignore_mismatched_sizes=True,
        )
        print(f"Model reloaded from '{save_path}' with correct LayerNorm keys.")
    else:
        print("LayerNorm keys already use the new format (weight/bias). No renaming needed.")

    return model


def sanitize_text(text: str) -> str:
    """Normalize whitespace in the input text."""
    if not isinstance(text, str):
        text = "" if pd.isna(text) else str(text)
    return re.sub(r"\s+", " ", text).strip()


def load_dataset_from_csv(csv_path: Path) -> pd.DataFrame:
    """Load and validate the training data CSV."""
    if not csv_path.exists():
        raise FileNotFoundError(
            f"CSV file '{csv_path}' not found. "
            f"Create a CSV with columns 'text' and 'label' (0=Non-abusive, 1=Abusive)."
        )

    with csv_path.open("r", encoding="utf-8", newline="") as csv_file:
        dataset_pandas = pd.DataFrame(csv.DictReader(csv_file))

    required_columns = {"text", "label"}
    missing_columns = required_columns - set(dataset_pandas.columns)
    if missing_columns:
        raise ValueError(
            f"CSV must contain columns {required_columns}. Missing: {missing_columns}"
        )

    dataset_pandas = dataset_pandas[["text", "label"]].copy()
    dataset_pandas["text"] = dataset_pandas["text"].map(sanitize_text)
    dataset_pandas = dataset_pandas[dataset_pandas["text"].str.len() > 0]
    dataset_pandas["label"] = pd.to_numeric(dataset_pandas["label"], errors="raise").astype(int)

    invalid_labels = sorted(set(dataset_pandas["label"].unique()) - {0, 1})
    if invalid_labels:
        raise ValueError(
            f"Invalid labels detected: {invalid_labels}. Only 0 and 1 are allowed."
        )

    return dataset_pandas.sample(frac=1.0, random_state=SEED).reset_index(drop=True)


def group_split(dataset_pandas: pd.DataFrame, test_size: float = 0.2):
    """Group-based split to prevent near-duplicate leakage between train and test.

    The dataset contains ~5400 rows that belong to groups of nearly identical texts
    (variations of the same template). A naive random split would place variant A
    in train and variant B in test, yielding an artificially inflated F1 (~1.0)
    that does not reflect real-world performance on unseen clauses.

    """
    # Use the first 100 characters as the group key
    group_keys = dataset_pandas["text"].str[:100]
    unique_groups = group_keys.unique()
    group_map = {g: i for i, g in enumerate(unique_groups)}
    groups = group_keys.map(group_map).values

    n_unique_groups = len(unique_groups)
    n_total = len(dataset_pandas)
    print(f"Unique template groups detected: {n_unique_groups} out of {n_total} examples.")
    print(
        f"Examples in groups with duplicates: "
        f"{(group_keys.map(group_keys.value_counts()) > 1).sum()} "
        f"({(group_keys.map(group_keys.value_counts()) > 1).mean() * 100:.1f}%)"
    )

    gss = GroupShuffleSplit(n_splits=1, test_size=test_size, random_state=SEED)
    train_idx, test_idx = next(gss.split(dataset_pandas, groups=groups))

    train_df = dataset_pandas.iloc[train_idx].copy().reset_index(drop=True)
    test_df = dataset_pandas.iloc[test_idx].copy().reset_index(drop=True)

    # Verify: no group should appear in both sets
    train_groups = set(group_keys.iloc[train_idx])
    test_groups = set(group_keys.iloc[test_idx])
    overlap = train_groups & test_groups
    if overlap:
        print(
            f"WARNING: {len(overlap)} groups appear in both sets. "
            f"Review the grouping logic."
        )
    else:
        print("Leakage check: OK — no template appears in both sets.")

    return train_df, test_df


def tokenize_function(examples, tokenizer):
    """Tokenize text inputs for the model."""
    return tokenizer(
        examples["text"],
        padding="max_length",
        truncation=True,
        max_length=MAX_LENGTH,
    )


def compute_metrics(pred):
    """Compute evaluation metrics (accuracy, F1, precision, recall)."""
    labels = pred.label_ids
    preds = pred.predictions.argmax(-1)
    precision, recall, f1, _ = precision_recall_fscore_support(
        labels, preds, average="binary", zero_division=0
    )
    acc = accuracy_score(labels, preds)
    return {
        "accuracy": acc,
        "f1": f1,
        "precision": precision,
        "recall": recall,
    }


def train_model(
    csv_path: str,
    model_name: str,
    output_dir: str,
    smoke_test: bool,
    check_only: bool,
) -> None:
    dataset_pandas = load_dataset_from_csv(Path(csv_path))
    print(f"Total examples after sanitization: {len(dataset_pandas)}")
    print(
        f"Label distribution: "
        f"{dataset_pandas['label'].value_counts().sort_index().to_dict()}"
    )

    # Group-based split — prevents near-duplicate leakage
    train_df, test_df = group_split(dataset_pandas, test_size=0.2)

    print(
        f"Train label distribution: "
        f"{train_df['label'].value_counts(normalize=True).sort_index().round(4).to_dict()}"
    )
    print(
        f"Test label distribution:  "
        f"{test_df['label'].value_counts(normalize=True).sort_index().round(4).to_dict()}"
    )

    if smoke_test:
        train_df = train_df.head(min(32, len(train_df))).copy()
        test_df = test_df.head(min(16, len(test_df))).copy()
        print(f"Smoke test active: train={len(train_df)}, test={len(test_df)}")

    if check_only:
        print("Dataset validation passed. Skipping training (--check-only).")
        return

    dataset_split = {
        "train": Dataset.from_pandas(train_df, preserve_index=False),
        "test": Dataset.from_pandas(test_df, preserve_index=False),
    }

    print("Loading tokenizer...")
    tokenizer = AutoTokenizer.from_pretrained(model_name)

    print("Tokenizing data...")
    tokenized_datasets = {
        "train": dataset_split["train"].map(
            lambda x: tokenize_function(x, tokenizer), batched=True
        ),
        "test": dataset_split["test"].map(
            lambda x: tokenize_function(x, tokenizer), batched=True
        ),
    }

    print("Loading base model...")
    model = AutoModelForSequenceClassification.from_pretrained(
        model_name,
        num_labels=2,
        ignore_mismatched_sizes=True,
    )
    model.config.id2label = {0: "Neabuziv", 1: "Abuziv"}
    model.config.label2id = {"Neabuziv": 0, "Abuziv": 1}

    # Fix LayerNorm: rename gamma/beta → weight/bias and save to disk
    # before Trainer takes over, so checkpoints will use correct keys.
    layernorm_fix_path = str(Path(output_dir).parent / "_layernorm_fix_temp")
    model = fix_and_save_layernorm(model, layernorm_fix_path)

    # Copy tokenizer to the temp folder (needed for reload)
    tokenizer.save_pretrained(layernorm_fix_path)

    num_epochs = 1 if smoke_test else 3  # 3 epochs suffice; early stopping halts sooner if needed

    training_args = TrainingArguments(
        output_dir="./rezultate_antrenament",
        num_train_epochs=num_epochs,
        per_device_train_batch_size=8,
        per_device_eval_batch_size=8,
        learning_rate=2e-5,
        warmup_ratio=0.1,          # 10% warmup — more stable than a fixed warmup_steps
        weight_decay=0.01,
        logging_dir="./logs",
        logging_steps=1 if smoke_test else 50,
        eval_strategy="epoch",
        save_strategy="epoch",
        save_total_limit=2,
        load_best_model_at_end=not smoke_test,
        metric_for_best_model="eval_loss",
        greater_is_better=False,
        report_to="none",
        seed=SEED,
    )

    callbacks = (
        [EarlyStoppingCallback(early_stopping_patience=2)] if not smoke_test else None
    )

    trainer = Trainer(
        model=model,
        args=training_args,
        train_dataset=tokenized_datasets["train"],
        eval_dataset=tokenized_datasets["test"],
        compute_metrics=compute_metrics,
        callbacks=callbacks,
    )

    print(
        f"Starting training (fine-tuning) — max {num_epochs} epochs with early stopping..."
    )
    trainer.train()

    model = trainer.model

    # Diagnostic: check whether the model collapsed to a single class
    eval_output = trainer.predict(tokenized_datasets["test"])
    eval_preds = np.argmax(eval_output.predictions, axis=-1)
    unique_pred, counts_pred = np.unique(eval_preds, return_counts=True)
    pred_distribution = {int(k): int(v) for k, v in zip(unique_pred, counts_pred)}
    print(f"Prediction distribution on the test set: {pred_distribution}")

    if len(pred_distribution) == 1:
        only_class = next(iter(pred_distribution))
        print(
            f"WARNING: model predicts only one class on test (class={only_class}). "
            f"Review the data and hyperparameters."
        )

    # Save final model
    Path(output_dir).mkdir(parents=True, exist_ok=True)
    model.save_pretrained(output_dir)
    tokenizer.save_pretrained(output_dir)

    # Post-save fix: rename gamma/beta → weight/bias directly in the safetensors file.
    # Trainer saves with the model's internal keys, which retain the old naming convention
    # from the original checkpoint (dumitrescustefan). The fix must be applied on disk.
    safetensors_path = Path(output_dir) / "model.safetensors"
    if safetensors_path.exists():
        raw = load_file(str(safetensors_path))
        fixed = {}
        n_renamed = 0
        for k, v in raw.items():
            new_k = k
            if "LayerNorm.gamma" in k:
                new_k = k.replace("LayerNorm.gamma", "LayerNorm.weight")
                n_renamed += 1
            elif "LayerNorm.beta" in k:
                new_k = k.replace("LayerNorm.beta", "LayerNorm.bias")
                n_renamed += 1
            fixed[new_k] = v
        if n_renamed > 0:
            save_file(fixed, str(safetensors_path))
            print(f"Post-save fix: {n_renamed} LayerNorm keys renamed on disk.")
        else:
            print("Post-save: LayerNorm keys are already correct.")

    print(f"Training complete. Model saved to '{output_dir}'.")

    # Clean up the temporary folder used for the LayerNorm fix
    if Path(layernorm_fix_path).exists():
        shutil.rmtree(layernorm_fix_path)
        print(f"Temporary folder '{layernorm_fix_path}' deleted.")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="Train an abusive clause classifier.")
    parser.add_argument(
        "--csv",
        default="date_clauze.csv",
        help="Path to the CSV with columns text,label",
    )
    parser.add_argument(
        "--model-name",
        default="dumitrescustefan/bert-base-romanian-cased-v1",
        help="HuggingFace model name or local path",
    )
    parser.add_argument(
        "--output-dir",
        default="./model_clauze_abuzive",
        help="Directory to save the final model",
    )
    parser.add_argument(
        "--smoke-test",
        action="store_true",
        help="Run a short training on a small subset",
    )
    parser.add_argument(
        "--check-only",
        action="store_true",
        help="Validate the data and split without training",
    )
    args = parser.parse_args()

    train_model(
        csv_path=args.csv,
        model_name=args.model_name,
        output_dir=args.output_dir,
        smoke_test=args.smoke_test,
        check_only=args.check_only,
    )