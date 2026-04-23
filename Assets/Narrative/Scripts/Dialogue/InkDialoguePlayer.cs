using System.Collections.Generic;
using Ink.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class InkDialoguePlayer : MonoBehaviour
{
    [Header("Ink")]
    [SerializeField] private TextAsset inkJSON;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI dialogueText;
    [SerializeField] private Transform choicesRoot;
    [SerializeField] private Button choiceButtonPrefab;

    private Story story;
    private readonly List<Button> currentButtons = new();

    private bool waitingForChoice = false;

    void Start()
    {
        StartStory();
    }

    void Update()
    {
        if (story == null)
            return;

        // On ignore le clic global si on est en train d'attendre un vrai choix
        if (waitingForChoice)
            return;

        // Clic gauche n'importe où
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            AdvanceStory();
        }
    }

    public void StartStory()
    {
        if (inkJSON == null)
        {
            Debug.LogError("Ink JSON est NULL");
            return;
        }

        story = new Story(inkJSON.text);
        story.onError += (msg, type) =>
        {
            if (type == Ink.ErrorType.Warning)
                Debug.LogWarning(msg);
            else
                Debug.LogError(msg);
        };

        dialogueText.text = "";
        ClearChoices();
        waitingForChoice = false;

        AdvanceStory();
    }

    private void AdvanceStory()
    {
        if (story == null)
            return;

        // S'il reste du texte, on n'affiche qu'une seule "étape"
        if (story.canContinue)
        {
            string nextLine = story.Continue().Trim();

            // On saute les lignes vides éventuelles
            while (string.IsNullOrEmpty(nextLine) && story.canContinue)
            {
                nextLine = story.Continue().Trim();
            }

            if (!string.IsNullOrEmpty(nextLine))
            {
                dialogueText.text = nextLine;
            }
        }

        RefreshChoicesState();
    }

    private void RefreshChoicesState()
    {
        ClearChoices();

        if (story.currentChoices.Count > 0)
        {
            waitingForChoice = true;

            for (int i = 0; i < story.currentChoices.Count; i++)
            {
                Choice choice = story.currentChoices[i];
                Button button = Instantiate(choiceButtonPrefab, choicesRoot);
                currentButtons.Add(button);

                TextMeshProUGUI buttonText = button.GetComponentInChildren<TextMeshProUGUI>();
                if (buttonText != null)
                    buttonText.text = choice.text;

                int index = i;
                button.onClick.AddListener(() => OnClickChoice(index));
            }
        }
        else
        {
            waitingForChoice = false;
        }
    }

    private void OnClickChoice(int index)
    {
        story.ChooseChoiceIndex(index);
        ClearChoices();
        waitingForChoice = false;
        AdvanceStory();
    }

    private void ClearChoices()
    {
        foreach (Button btn in currentButtons)
        {
            if (btn != null)
                Destroy(btn.gameObject);
        }

        currentButtons.Clear();
    }
}