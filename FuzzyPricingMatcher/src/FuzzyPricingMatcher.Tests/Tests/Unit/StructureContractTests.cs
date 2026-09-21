using NUnit.Framework;

namespace FuzzyPricingMatcher.Tests.Tests.Unit;

public sealed class StructureContractTests
{
    [Test]
    [Category("Unit")]
    public void Approved_top_level_folders_exist_and_legacy_names_are_absent()
    {
        var projectDirectory = FindProjectDirectory();
        var sourceDirectory = projectDirectory;

        Assert.Multiple(() =>
        {
            Assert.That(Directory.Exists(Path.Combine(sourceDirectory, "ExternalAPIAccess")), Is.True);
            Assert.That(Directory.Exists(Path.Combine(sourceDirectory, "ExternalPricing")), Is.False);
            Assert.That(Directory.Exists(Path.Combine(sourceDirectory, "Api")), Is.False);
            Assert.That(Directory.Exists(Path.Combine(sourceDirectory, "ExternalServices")), Is.False);
            Assert.That(Directory.Exists(Path.Combine(sourceDirectory, "ExternalServiceAccess")), Is.False);
            Assert.That(Directory.Exists(Path.Combine(sourceDirectory, "Database")), Is.True);
            Assert.That(Directory.Exists(Path.Combine(sourceDirectory, "Data")), Is.False);
            Assert.That(Directory.Exists(Path.Combine(sourceDirectory, "Persistence")), Is.False);
            Assert.That(Directory.Exists(Path.Combine(sourceDirectory, "Tests")), Is.True);
            Assert.That(Directory.Exists(Path.Combine(FindSolutionRoot(projectDirectory), "TestAsset")), Is.True);
        });
    }

    [Test]
    [Category("Unit")]
    public void External_and_database_source_files_remain_in_their_approved_boundaries()
    {
        var projectDirectory = FindProjectDirectory();
        var externalFiles = Directory.GetFiles(Path.Combine(projectDirectory, "ExternalAPIAccess"), "*.cs", SearchOption.AllDirectories);
        var databaseFiles = Directory.GetFiles(Path.Combine(projectDirectory, "Database"), "*.cs", SearchOption.AllDirectories);

        Assert.Multiple(() =>
        {
            Assert.That(externalFiles.Any(path => Path.GetFileName(path) == "ExternalXmlServiceClient.cs"), Is.True);
            Assert.That(externalFiles.Any(path => Path.GetFileName(path) == "ExternalServiceClientFactory.cs"), Is.True);
            Assert.That(databaseFiles.Any(path => Path.GetFileName(path) == "FuzzyMatcherRepository.cs"), Is.True);
            Assert.That(databaseFiles.Any(path => Path.GetFileName(path) == "SqlMutationExecutor.cs"), Is.True);
        });
    }

    [Test]
    [Category("Unit")]
    public void Loader_and_comparison_sources_do_not_reference_transport_or_sql_implementation_types()
    {
        var projectDirectory = FindProjectDirectory();
        var workflowFiles = Directory.GetFiles(projectDirectory, "*.cs", SearchOption.TopDirectoryOnly)
            .Concat(Directory.GetFiles(Path.Combine(projectDirectory, "Loader"), "*.cs", SearchOption.AllDirectories))
            .Concat(Directory.GetFiles(Path.Combine(projectDirectory, "Comparison"), "*.cs", SearchOption.AllDirectories));
        var workflowText = string.Join(Environment.NewLine, workflowFiles.Select(File.ReadAllText));

        Assert.Multiple(() =>
        {
            Assert.That(workflowText, Does.Not.Contain("using RestSharp"));
            Assert.That(workflowText, Does.Not.Contain("SELECT "));
            Assert.That(workflowText, Does.Not.Contain("INSERT "));
            Assert.That(workflowText, Does.Not.Contain("UPDATE "));
            Assert.That(workflowText, Does.Not.Contain("DELETE "));
        });
    }

    [Test]
    [Category("Unit")]
    public void Solution_contains_exactly_one_non_generated_project()
    {
        var solutionRoot = FindSolutionRoot(FindProjectDirectory());
        var projectFiles = Directory.GetFiles(solutionRoot, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !path.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.That(projectFiles, Has.Length.EqualTo(1));
    }

    private static string FindProjectDirectory()
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "FuzzyPricingMatcher.Tests.csproj")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("The test project directory could not be located.");
    }

    private static string FindSolutionRoot(string projectDirectory)
        => Directory.GetParent(Directory.GetParent(projectDirectory)!.FullName)!.FullName;
}
