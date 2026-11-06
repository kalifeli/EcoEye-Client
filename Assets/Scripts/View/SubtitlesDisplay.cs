using UnityEngine;
using TMPro;

public class SubtitlesDisplay : MonoBehaviour
{
    [SerializeField]
    private TMP_Text subtitleText;

    [SerializeField]
    private string waitingMessage = "In attesa di una connversazione...";


    private void Awake()
    {
        if(subtitleText == null)
        {
            Debug.LogError("SubtitleText non assegnato...");
            enabled = false; // disattiva il componente SubtitleDisplay per evitare chiamate inutili a Update
            return; // serve per interrompere l'esecuzione del metodo corrente 
        }

        subtitleText.richText = false;
        subtitleText.raycastTarget = false;

        ShowSubtitle(waitingMessage);
    }

    public void ShowSubtitle(string subtitle)
    {
        if (subtitleText == null)
        {
           return;
        }

        subtitleText.SetText(subtitle ?? string.Empty);

    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
