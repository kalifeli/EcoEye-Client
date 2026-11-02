using System;

[Serializable]
public class EcoEyeMessage
{
    public string type;
    public string content;

    public EcoEyeMessage() {}

    public EcoEyeMessage(string type, string content)
    {
        this.type = type;
        this.content = content ?? "";
    }
}