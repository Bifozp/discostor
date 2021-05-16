using System.IO;
using System.Text.Json;

namespace Impostor.Plugins.Discostor.Config
{
    public class ConfigBuilder<T> where T : new()
    {
        private string Dir
        {
            get
            {
                // Based on the current directory,
                // according to the specification of Impostor server.
                return Path.Combine(Directory.GetCurrentDirectory(), "config");
            }
        }

        public T Build(string jsonName)
        {
            if(!Directory.Exists(Dir))
            {
                Directory.CreateDirectory(Dir);
            }

            var jsonPath = Path.Combine(Dir, jsonName);
            try {
                var json = File.ReadAllText(jsonPath);
                return JsonSerializer.Deserialize<T>(json) ?? new T();
            }
            catch (FileNotFoundException)
            {   // Create base file.
                var config = new T();
                var option = new JsonSerializerOptions(){ WriteIndented = true };
                var json = JsonSerializer.Serialize(config, option);
                File.WriteAllText(jsonPath, json);
                return config;
            }
        }
    }
}
