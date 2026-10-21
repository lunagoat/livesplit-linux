using System;
using System.Xml;

namespace LiveSplit.Options;

public abstract class RaceProviderSettings : ICloneable
{
    public bool Enabled = true;
    public abstract string Name { get; set; }
    public abstract string DisplayName { get; }
    public abstract string WebsiteLink { get; }
    public abstract string RulesLink { get; }

    public abstract object Clone();

    public virtual string SettingsMessage => "There are no options available for this racing service.";

    public virtual object GetSettingsControl()
    {
        return null;
    }

    public virtual void FromXml(XmlElement element, Version version)
    {
        XmlAttribute enabled = element.Attributes["enabled"];
        if (!bool.TryParse(enabled.Value, out Enabled))
        {
            Enabled = true;
        }
    }

    public virtual XmlElement ToXml(XmlDocument document)
    {
        XmlElement parent = document.CreateElement("Plugin");

        XmlAttribute providerName = document.CreateAttribute("name");
        providerName?.InnerText = Name.ToString();

        parent.Attributes.Append(providerName);

        XmlAttribute enabled = document.CreateAttribute("enabled");
        enabled?.InnerText = Enabled.ToString();

        parent.Attributes.Append(enabled);

        return parent;
    }
}

public class UnloadedRaceProviderSettings : RaceProviderSettings
{
    public override string Name { get; set; }
    public override string DisplayName => Name;
    private string Content { get; set; }
    public override string WebsiteLink => "";
    public override string RulesLink => "";

    public override object Clone()
    {
        return new UnloadedRaceProviderSettings()
        {
            Enabled = Enabled,
            Name = Name,
            Content = Content
        };
    }

    public override string SettingsMessage => "Plugin could not be loaded";

    public override void FromXml(XmlElement element, Version version)
    {
        base.FromXml(element, version);

        Content = element.InnerXml;

        if (element.Attributes["name"] != null)
        {
            Name = element.Attributes["name"].InnerText;
        }
    }

    public override XmlElement ToXml(XmlDocument document)
    {
        XmlElement element = base.ToXml(document);

        if (element.Attributes["name"] == null)
        {
            element.Attributes.Append(document.CreateAttribute("name"));
        }

        element.Attributes["name"].Value = Name;

        element.InnerXml = Content;
        return element;
    }
}
