import torch
import re
from transformers import AutoTokenizer, AutoModelForSequenceClassification

class ClasificatorClauzeAbuzive:
    def __init__(self, model_path="dumitrescustefan/bert-base-romanian-cased-v1"):
        """
        Initialize the classifier.

        If model_path points to the base HuggingFace model, predictions will be
        random until the model is fine-tuned. After training, set model_path to the
        directory where you saved the trained model (e.g. './model_antrenat_clauze').
        """
        self.model_path = model_path

        # Load the tokenizer and model
        # num_labels=2 for binary classification (Abusive / Non-abusive)
        print(f"Loading model from: {self.model_path}...")
        self.tokenizer = AutoTokenizer.from_pretrained(self.model_path)
        self.model = AutoModelForSequenceClassification.from_pretrained(
            self.model_path,
            num_labels=2
        )
        print("Config id2label:", self.model.config.id2label)
        print("Config architecture:", self.model.config.architectures)

        # Move the model to GPU if available, otherwise CPU
        self.device = torch.device("cuda" if torch.cuda.is_available() else "cpu")
        self.model.to(self.device)

        # Set the model to evaluation mode (disables dropout, etc.)
        self.model.eval()

        # Use the label mapping saved in the model config; fallback if missing
        config_id2label = getattr(self.model.config, "id2label", None)
        if isinstance(config_id2label, dict) and 0 in config_id2label and 1 in config_id2label:
            self.id2label = {0: str(config_id2label[0]), 1: str(config_id2label[1])}
        else:
            self.id2label = {0: "Neabuziv", 1: "Abuziv"}

    @staticmethod
    def _sanitize_text(text: str) -> str:
        if not isinstance(text, str):
            text = str(text)
        return re.sub(r"\s+", " ", text).strip()

    def _predict(self, text: str):
        """Run a single inference pass and return sanitized text + class probabilities."""
        sanitized_text = self._sanitize_text(text)
        if not sanitized_text:
            raise ValueError("Textul clauzei nu poate fi gol.")

        # Tokenize the input text
        inputs = self.tokenizer(
            sanitized_text,
            return_tensors="pt",
            truncation=True,
            padding=True,
            max_length=512  # BERT supports a maximum of 512 tokens
        )

        # Move inputs to the same device as the model
        inputs = {k: v.to(self.device) for k, v in inputs.items()}

        # Run inference (no gradient computation for efficiency)
        with torch.no_grad():
            outputs = self.model(**inputs)

        # Apply softmax to get probabilities between 0 and 1
        probabilities = torch.nn.functional.softmax(outputs.logits, dim=-1)
        predicted_class_id = torch.argmax(probabilities, dim=-1).item()

        return (
            sanitized_text,
            predicted_class_id,
            probabilities[0][1].item(),
            probabilities[0][0].item(),
        )

    def clasifica(self, text: str) -> dict:
        """
        Takes a text (contractual clause) and returns the predicted class and probabilities.
        """
        sanitized_text, predicted_class_id, prob_abuziv, prob_neabuziv = self._predict(text)

        # Return a dictionary with the results
        return {
            "text": sanitized_text,
            "clasa_prezisa": self.id2label[predicted_class_id],
            "probabilitate_abuziv": round(prob_abuziv * 100, 2),
            "probabilitate_neabuziv": round(prob_neabuziv * 100, 2)
        }

    def clasifica_api(self, text: str) -> dict:
        """Return API-friendly JSON payload: binary label and abusive probability (0..1)."""
        _, predicted_class_id, prob_abuziv, _ = self._predict(text)
        return {
            "label": int(predicted_class_id),
            "probabilitate_abuziv": round(float(prob_abuziv), 4),
        }

# --- Usage example ---
if __name__ == "__main__":
    # Initialize the classifier
    clasificator = ClasificatorClauzeAbuzive(model_path="./model_clauze_abuzive")

    clauze = [
    "Banca își rezervă dreptul de a modifica unilateral rata dobânzii și comisioanele, fără o notificare prealabilă a clientului.",
    "Prezentul contract se încheie pe o perioadă de 12 luni, cu posibilitatea prelungirii prin act adițional semnat de ambele părți.",
    "Prestatorul poate suspenda furnizarea serviciilor fără nicio notificare în cazul în care consideră, la discreția sa, că utilizatorul a încălcat orice regulă.",
    "În caz de forță majoră, niciuna dintre părți nu va fi trasă la răspundere pentru neîndeplinirea obligațiilor contractuale, sub rezerva notificării în termen de 5 zile.",
    "Clientul renunță în mod expres la dreptul de a contesta în instanță sumele facturate de către furnizor.",
    "Orice modificare a tarifelor va fi comunicată clientului cu cel puțin 30 de zile înainte de intrarea în vigoare, clientul având dreptul de a rezilia contractul fără penalități dacă nu este de acord.",
    "Sumele plătite în avans nu se restituie sub nicio formă, chiar dacă serviciul nu a fost prestat din vina exclusivă a companiei.",
    "Plata facturilor se va efectua în termen de 14 zile calendaristice de la data emiterii acestora.",
    "Vânzătorul are dreptul exclusiv de a interpreta clauzele prezentului contract în caz de neînțelegeri între părți.",
    "Prezentul contract poate fi denunțat unilateral de oricare dintre părți, printr-o notificare scrisă transmisă cu 30 de zile înainte.",
    "Compania nu este responsabilă pentru nicio daună directă sau indirectă cauzată de produsele sale, indiferent de circumstanțe și de culpa companiei.",
    "Datele cu caracter personal vor fi prelucrate în conformitate cu prevederile Regulamentului (UE) 2016/679 (GDPR).",
    "Dacă bunul livrat prezintă defecte, singura opțiune a consumatorului este acceptarea reparației acestuia, fără a putea solicita înlocuirea sau rambursarea banilor.",
    "Garanția comercială a produsului este de 24 de luni de la data achiziționării, acoperind exclusiv defectele de fabricație.",
    "Neplata unei singure rate la scadență atrage după sine penalități de 5% pe zi de întârziere din suma datorată, putând depăși debitul principal.",
    "Litigiile decurgând din interpretarea sau executarea prezentului contract vor fi soluționate pe cale amiabilă, iar în caz de eșec, de către instanțele judecătorești competente.",
    "Termenul de livrare estimat este de 10 zile, însă vânzătorul își rezervă dreptul de a-l prelungi pe termen nedeterminat fără acordul cumpărătorului.",
    "În cazul în care produsul nu este disponibil, vânzătorul va informa cumpărătorul în termen de 3 zile lucrătoare și va returna integral suma plătită în maximum 7 zile.",
    "În caz de reziliere anticipată, clientul este obligat să achite un comision cu titlu de daune-interese echivalent cu contravaloarea tuturor abonamentelor rămase până la finalul perioadei contractuale.",
    "Consumatorul beneficiază de o perioadă de 14 zile calendaristice pentru a se retrage din contractul încheiat la distanță, fără a fi nevoit să justifice decizia de retragere.",
    "Oricare dintre parti poate rezilia prezentul contract printr-o notificare scrisa transmisa celeilalte parti cu cel putin 30 de zile inainte de data la care rezilierea produce efecte.",
    "In cazul in care livrarea intarzie din motive neimputabile vanzatorului, acesta va notifica cumparatorul in scris, oferind un nou termen de livrare care nu va depasi 30 de zile.",
    "Prețul abonamentului poate fi indexat anual cu rata oficială a inflației comunicată de Institutul Național de Statistică. Prestatorul va notifica Clientul cu cel puțin 30 de zile înainte de aplicarea modificării, iar Clientul are dreptul de a rezilia contractul fără nicio penalitate în termen de 15 zile de la primirea notificării, dacă nu acceptă noul preț.",
    "Banca/Compania își rezervă dreptul de a modifica tarifele, comisioanele sau structura serviciilor oferite oricând pe parcursul executării contractului, fără acordul prealabil al Clientului, noile prețuri devenind obligatorii de la data publicării lor pe site-ul propriu"
    ]

    for c in clauze:
        r = clasificator.clasifica(c)
        print(f"[{r['clasa_prezisa']}] {r['probabilitate_abuziv']}% abuziv — {c[:60]}...")
