using System.Collections.Concurrent;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Linq;

namespace FuzzyPricingMatcher.Tests.Validation;

public sealed class SchemaRegistry : ISchemaRegistry
{
    private readonly string schemasDirectory;
    private readonly ConcurrentDictionary<string, Lazy<SchemaRegistration>> registrations = new(StringComparer.OrdinalIgnoreCase);

    public SchemaRegistry(string schemasDirectory, IEnumerable<string> schemaFileNames)
    {
        this.schemasDirectory = Path.GetFullPath(schemasDirectory);
        var names = schemaFileNames.ToArray();
        if (names.Length != names.Distinct(StringComparer.OrdinalIgnoreCase).Count())
            throw new DuplicateSchemaRegistrationException("Duplicate schema registrations are not allowed.");
        foreach (var name in names)
        {
            var fullPath = Path.GetFullPath(Path.Combine(this.schemasDirectory, name));
            if (!fullPath.StartsWith(this.schemasDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new SchemaRegistryException($"Schema path '{name}' escapes the trusted schema directory.");
            registrations[name] = new Lazy<SchemaRegistration>(() => Compile(name, fullPath), LazyThreadSafetyMode.ExecutionAndPublication);
        }
    }

    public SchemaRegistration Resolve(string schemaFileName)
    {
        if (!registrations.TryGetValue(schemaFileName, out var registration))
            throw new SchemaFileNotFoundException($"Schema '{schemaFileName}' is not registered.");
        return registration.Value;
    }

    private static SchemaRegistration Compile(string name, string fullPath)
    {
        if (!File.Exists(fullPath))
            throw new SchemaFileNotFoundException($"Schema file '{fullPath}' was not found.");
        try
        {
            var schemaDocument = XDocument.Load(fullPath, LoadOptions.PreserveWhitespace);
            foreach (var reference in schemaDocument.Descendants().Where(element => element.Name.LocalName is "include" or "import"))
            {
                var location = reference.Attribute("schemaLocation")?.Value;
                if (string.IsNullOrWhiteSpace(location))
                    continue;
                if (Uri.TryCreate(location, UriKind.Absolute, out var uri) && !string.Equals(uri.Scheme, Uri.UriSchemeFile, StringComparison.OrdinalIgnoreCase))
                    throw new SchemaCompilationException($"External schema resolution is disabled for '{location}'.");
            }
            var schemas = new XmlSchemaSet { XmlResolver = new LocalSchemaResolver(Path.GetDirectoryName(fullPath)!) };
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
            using var reader = XmlReader.Create(fullPath, settings);
            schemas.Add(null, reader);
            schemas.Compile();
            return new SchemaRegistration(name, fullPath, schemas);
        }
        catch (SchemaRegistryException) { throw; }
        catch (Exception exception) { throw new SchemaCompilationException($"Schema '{fullPath}' could not be compiled: {exception.Message}", exception); }
    }

    private sealed class LocalSchemaResolver(string rootDirectory) : XmlResolver
    {
        private readonly string root = Path.GetFullPath(rootDirectory) + Path.DirectorySeparatorChar;

        public override System.Net.ICredentials? Credentials { set { } }

        public override Uri ResolveUri(Uri? baseUri, string? relativeUri)
        {
            var resolved = base.ResolveUri(baseUri, relativeUri);
            if (!string.Equals(resolved.Scheme, Uri.UriSchemeFile, StringComparison.OrdinalIgnoreCase))
                throw new XmlException("External schema resolution is disabled.");
            var fullPath = Path.GetFullPath(resolved.LocalPath);
            if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new XmlException("Schema include/import escapes the trusted schema directory.");
            return new Uri(fullPath);
        }

        public override object GetEntity(Uri absoluteUri, string? role, Type? ofObjectToReturn)
        {
            if (!string.Equals(absoluteUri.Scheme, Uri.UriSchemeFile, StringComparison.OrdinalIgnoreCase))
                throw new XmlException("External schema resolution is disabled.");
            return File.OpenRead(absoluteUri.LocalPath);
        }
    }
}