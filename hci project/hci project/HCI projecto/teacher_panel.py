import json
import os


WORDS_FILE = "words.json"

WORDS_DEFAULT = {
    "subjects": ["I", "You", "He", "She"],
    "verbs":    ["eat", "read", "kick", "drink"],
    "objects":  ["apple", "book", "ball", "milk"]
}


def load_words():
    if not os.path.exists(WORDS_FILE):
        save_words(WORDS_DEFAULT)
        return WORDS_DEFAULT
    try:
        with open(WORDS_FILE, "r", encoding="utf-8") as f:
            return json.load(f)
    except Exception as e:
        print("Words load error:", e)
        return WORDS_DEFAULT


def save_words(data):
    try:
        with open(WORDS_FILE, "w", encoding="utf-8") as f:
            json.dump(data, f, indent=4)
    except Exception as e:
        print("Words save error:", e)


def main():
    print("\n" + "=" * 40)
    print("       TEACHER PANEL")
    print("=" * 40)

    while True:
        words = load_words()

        print("\nCurrent word pools:")
        print("  Subjects:", words["subjects"])
        print("  Verbs:   ", words["verbs"])
        print("  Objects: ", words["objects"])

        print("\nOptions:")
        print("  1. Add a word")
        print("  2. Remove a word")
        print("  3. Update a word")
        print("  4. Exit")

        choice = input("\nChoose an option (1-4): ").strip()

        if choice == "1":
            category = input("Category (subjects / verbs / objects): ").strip().lower()
            if category not in words:
                print("Invalid category.")
                continue
            word = input(f"New word to add to {category}: ").strip()
            if word == "":
                print("Empty word, skipping.")
                continue
            if word.lower() in [w.lower() for w in words[category]]:
                print(f"'{word}' already exists in {category}.")
                continue
            words[category].append(word)
            save_words(words)
            print(f"Added '{word}' to {category}.")

        elif choice == "2":
            category = input("Category (subjects / verbs / objects): ").strip().lower()
            if category not in words:
                print("Invalid category.")
                continue
            print(f"Current {category}:", words[category])
            word = input("Word to remove: ").strip()
            match = next((w for w in words[category] if w.lower() == word.lower()), None)
            if match is None:
                print(f"'{word}' not found in {category}.")
                continue
            if len(words[category]) <= 1:
                print("Cannot remove the last word in a category.")
                continue
            words[category].remove(match)
            save_words(words)
            print(f"Removed '{match}' from {category}.")

        elif choice == "3":
            category = input("Category (subjects / verbs / objects): ").strip().lower()
            if category not in words:
                print("Invalid category.")
                continue
            print(f"Current {category}:", words[category])
            old_word = input("Word to replace: ").strip()
            match = next((w for w in words[category] if w.lower() == old_word.lower()), None)
            if match is None:
                print(f"'{old_word}' not found in {category}.")
                continue
            new_word = input(f"Replace '{match}' with: ").strip()
            if new_word == "":
                print("Empty word, skipping.")
                continue
            idx = words[category].index(match)
            words[category][idx] = new_word
            save_words(words)
            print(f"Updated '{match}' -> '{new_word}' in {category}.")

        elif choice == "4":
            print("Exiting teacher panel.")
            break

        else:
            print("Invalid option, try again.")


if __name__ == "__main__":
    main()
