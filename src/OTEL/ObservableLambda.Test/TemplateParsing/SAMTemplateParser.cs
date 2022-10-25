using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ObservableLambda.Test.TemplateParsing;

public static class SAMTemplateParser
{
    public static SamTemplate ParseTemplate(string templatePath)
    {
        var deserializer = new DeserializerBuilder()
            .Build();
        
        var p = deserializer.Deserialize<SamTemplate>(File.ReadAllText(templatePath));
        return p;
    }
}