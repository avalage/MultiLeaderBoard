using System.IO;
using System.Linq;
using System.Reflection;

namespace BeatLeader {
    public static class ResourcesUtils {
        public static string GetEmbeddedResourceText(string resourceName) {
            using var stream = GetEmbeddedResourceStream(resourceName);
            return new StreamReader(stream).ReadToEnd();
        }
        
        public static Stream GetEmbeddedResourceStream(string resourceName) {
            var assembly = typeof(Plugin).Assembly;
            var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream != null) {
                return stream;
            }

            var similarResources = assembly
                .GetManifestResourceNames()
                .Where(name => name.Contains("Resources") || name.Contains("_9_Resources"))
                .Take(25);

            throw new FileNotFoundException(
                $"Embedded resource '{resourceName}' was not found in assembly '{assembly.GetName().Name}'. " +
                $"Available resource samples: {string.Join(", ", similarResources)}",
                resourceName
            );
        }

        public static string[] GetEmbeddedResourceNames() {
            return typeof(Plugin).Assembly.GetManifestResourceNames();
        }
    }
}
