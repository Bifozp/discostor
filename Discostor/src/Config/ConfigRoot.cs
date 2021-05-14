using System.IO;
using System.Text.Json;

namespace Impostor.Plugins.Discostor.Config
{
    public class ConfigRoot
    {
        public DiscordBotConfig Discord { get; set; } = new DiscordBotConfig();
        public AmongusConfig Amongus { get; set; } = new AmongusConfig();

        private static string Dir
        {
            get
            {
                // Based on the current directory,
                // according to the specification of Impostor server.
                return Path.Combine(Directory.GetCurrentDirectory(), "config");
            }
        }

        public static ConfigRoot Build(string jsonName)
        {
            if(!Directory.Exists(ConfigRoot.Dir))
            {
                Directory.CreateDirectory(ConfigRoot.Dir);
            }

            var jsonPath = Path.Combine(ConfigRoot.Dir, jsonName);
            try {
                var json = File.ReadAllText(jsonPath);
                return JsonSerializer.Deserialize<ConfigRoot>(json) ?? new ConfigRoot();
            }
            catch (FileNotFoundException)
            {   // Create base file.
                var config = new ConfigRoot();
                var option = new JsonSerializerOptions(){ WriteIndented = true };
                var json = JsonSerializer.Serialize(config, option);
                File.WriteAllText(jsonPath, json);
                return config;
            }
        }
    }
}
