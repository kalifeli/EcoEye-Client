using UnityEngine;
using System.Collections;

public class SubtitleDemoSource : MonoBehaviour
{

    [SerializeField]
    private SubtitlesDisplay subtitlesDisplay;

    [SerializeField, Min(0.5f)]
    private float intervalSeconds = 3f;

    [SerializeField]
    private bool repeat = true;

    private readonly string[] demoMessages = new string[]
    {
        "Buongiorno, come stai?",

        "Benvenuti alla presentazione di EcoEye AR.",

        "Questo sistema permette di visualizzare il parlato.",

        "I sottotitoli vengono ricevuti in tempo reale.",

        "La trascrizione viene elaborata sullo smartphone.",

        "Il risultato viene mostrato sui Magic Leap 2."
    };


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if(subtitlesDisplay == null)
        {
            Debug.Log("EcoEye AR: Subtitle Display non assegnato");
            enabled = false;
            return;
        }

        StartCoroutine(SimulateTrascription());
    }

    // Update is called once per frame
    void Update()
    {
        
    }



    private IEnumerator SimulateTrascription()
    {
        do
        {
            foreach(string message in demoMessages)
            {
                subtitlesDisplay.ShowSubtitle(message);
                Debug.Log(message);
                yield return new WaitForSeconds(intervalSeconds);
            }
                
        }
        while(repeat);
    }
}