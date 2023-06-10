using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Windows.Media;
using Humanizer;

namespace mdresgen;

public static partial class Brushes
{
    [GeneratedRegex(@"^\s*<!-- INSERT HERE -->", RegexOptions.Multiline)]
    private static partial Regex TemplateReplaceRegex();

    private const string AutoGeneratedHeader = """
        //------------------------------------------------------------------------------
        // <auto-generated>
        //     This code was generated by mdresgen.
        // </auto-generated>
        //------------------------------------------------------------------------------

        #nullable enable
        """;
    private const string IgnoredBrushName = "MaterialDesign.Brush.Ignored";
    private const int CSharpIndentSize = 4;
    private const int XamlIndentSize = 2;

    public static async Task GenerateBrushesAsync()
    {
        await using var inputFile = File.OpenRead("ThemeColors.json");
        Brush[] brushes = await JsonSerializer.DeserializeAsync<Brush[]>(inputFile)
            ?? throw new InvalidOperationException("Did not find brushes from source file");

        brushes = brushes.OrderBy(x => x.Name).ToArray();

        var filteredBrushes = brushes.Where(x => x.Name != IgnoredBrushName).ToList();

        TreeItem<Brush> brushTree = BuildBrushTree(filteredBrushes);
        TreeItem<Brush> alternateBrushTree = BuildBrushTree(GetAllAlternateBrushesFlattened(filteredBrushes));

        DirectoryInfo repoRoot = GetRepoRoot() ?? throw new InvalidOperationException("Failed to find the repo root");

        GenerateBuiltInThemingDictionaries(brushes, repoRoot);
        GenerateObsoleteBrushesDictionary(filteredBrushes, repoRoot);
        GenerateThemeClass(alternateBrushTree, repoRoot);
        GenerateThemeExtensionsClass(alternateBrushTree, repoRoot);
        GenerateResourceDictionaryExtensions(alternateBrushTree, repoRoot);
        GenerateThemeBrushTests(alternateBrushTree, repoRoot);
    }

    private static void GenerateBuiltInThemingDictionaries(IEnumerable<Brush> brushes, DirectoryInfo repoRoot)
    {
        WriteFile("Light");
        WriteFile("Dark");

        void WriteFile(string theme)
        {
            using var writer = new StreamWriter(Path.Combine(repoRoot.FullName, "MaterialDesignThemes.Wpf", "Themes", $"MaterialDesignTheme.{theme}.xaml"));
            writer.WriteLine($"""
                <ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                                    xmlns:colors="clr-namespace:MaterialDesignColors;assembly=MaterialDesignColors"
                                    xmlns:po="http://schemas.microsoft.com/winfx/2006/xaml/presentation/options">
                  <ResourceDictionary.MergedDictionaries>
                    <ResourceDictionary Source="./Internal/MaterialDesignTheme.BaseThemeColors.xaml" />
                  </ResourceDictionary.MergedDictionaries>
                """);
            foreach (Brush brush in brushes)
            {
                string value = brush.ThemeValues[theme];
                WriteBrush(brush.Name!, value, writer);

                foreach (string alternate in brush.AlternateKeys ?? Enumerable.Empty<string>())
                {
                    WriteBrush(alternate, value, writer);
                }

                static void WriteBrush(string name, string value, StreamWriter writer)
                {
                    if (value.StartsWith('#'))
                    {
                        writer.WriteLine($$"""
                      <SolidColorBrush x:Key="{{name}}" Color="{{value}}" po:Freeze="True" />
                    """);
                    }
                    else
                    {
                        writer.WriteLine($$"""
                      <colors:StaticResource x:Key="{{name}}" ResourceKey="{{value}}" />
                    """);
                    }
                }
            }

            writer.WriteLine();

            writer.WriteLine("</ResourceDictionary>");
        }
    }

    private static void GenerateObsoleteBrushesDictionary(IEnumerable<Brush> brushes, DirectoryInfo repoRoot)
    {
        StringBuilder output = new();

        foreach (Brush brush in brushes)
        {
            foreach (string obsoleteKey in brush.ObsoleteKeys ?? Enumerable.Empty<string>())
            {
                output.AppendLine($$"""
                      <colors:StaticResource x:Key="{{obsoleteKey}}" ResourceKey="{{brush.Name}}" />
                    """);
            }
        }

        using var reader = new StreamReader("MaterialDesignTheme.ObsoleteBrushes.xaml");
        string existingDictionary = reader.ReadToEnd();
        string dictionaryContents = TemplateReplaceRegex().Replace(existingDictionary, output.ToString());

        using var writer = new StreamWriter(Path.Combine(repoRoot.FullName, "MaterialDesignThemes.Wpf", "Themes", "MaterialDesignTheme.ObsoleteBrushes.xaml"));
        writer.Write(dictionaryContents);
    }

    private static void GenerateThemeClass(TreeItem<Brush> brushes, DirectoryInfo repoRoot)
    {
        using var writer = new StreamWriter(Path.Combine(repoRoot.FullName, "MaterialDesignThemes.Wpf", "Theme.g.cs"));

        writer.WriteLine(AutoGeneratedHeader);
        writer.WriteLine("""
            using System.Windows.Media;

            namespace MaterialDesignThemes.Wpf;

            partial class Theme
            {
            """);

        WriteTreeItem(brushes, writer, 0);

        writer.WriteLine("}");

        static void WriteTreeItem(TreeItem<Brush> treeItem, StreamWriter writer, int indentLevel)
        {
            string indent = new(' ', indentLevel * CSharpIndentSize);
            if (!string.IsNullOrWhiteSpace(treeItem.Name))
            {
                writer.WriteLine($"{indent}public class {treeItem.Name}");
                writer.WriteLine($"{indent}{{");
            }

            foreach (Brush brush in treeItem.Values)
            {
                writer.WriteLine($"{indent}    public Color {brush.PropertyName} {{ get; set; }}");
                writer.WriteLine();
            }

            foreach (TreeItem<Brush> child in treeItem.Children)
            {
                writer.WriteLine($"{indent}    public {child.Name} {child.Name.Pluralize()} {{ get; set; }} = new();");
                writer.WriteLine();
            }

            foreach (TreeItem<Brush> child in treeItem.Children)
            {
                WriteTreeItem(child, writer, indentLevel + 1);
            }

            if (!string.IsNullOrWhiteSpace(treeItem.Name))
            {
                writer.WriteLine($"{indent}}}");
                writer.WriteLine();
            }
        }
    }

    private static void GenerateThemeExtensionsClass(TreeItem<Brush> brushes, DirectoryInfo repoRoot)
    {
        using var writer = new StreamWriter(Path.Combine(repoRoot.FullName, "MaterialDesignThemes.Wpf", "ThemeExtensions.g.cs"));

        writer.WriteLine(AutoGeneratedHeader);
        writer.WriteLine("""
            using System.Windows.Media;

            using MaterialDesignThemes.Wpf.Themes.Internal;

            namespace MaterialDesignThemes.Wpf;

            static partial class ThemeExtensions
            {
            """);

        WriteSetTheme(brushes, writer, "Light");
        writer.WriteLine();
        WriteSetTheme(brushes, writer, "Dark");

        writer.WriteLine("}");

        static void WriteSetTheme(TreeItem<Brush> treeItem, StreamWriter writer, string theme)
        {
            string indent = new(' ', CSharpIndentSize);
            writer.WriteLine($$"""
                {{indent}}public static partial void Set{{theme}}Theme(this Theme theme)
                {{indent}}{
                """);

            WriteTreeItem(treeItem, writer, theme.ToLowerInvariant(), "theme.");

            writer.WriteLine($"{indent}}}");
        }

        static void WriteTreeItem(TreeItem<Brush> treeItem, StreamWriter writer, string theme, string propertyPrefix)
        {
            string indent = new(' ', CSharpIndentSize);
            foreach (Brush brush in treeItem.Values)
            {
                string value = brush.ThemeValues[theme];
                if (value.StartsWith("#", StringComparison.Ordinal))
                {
                    Color color = (Color)TypeDescriptor.GetConverter(typeof(Color)).ConvertFromString(value)!;
                    writer.WriteLine($$"""
                        {{indent}}{{indent}}{{propertyPrefix}}{{brush.PropertyName}} = Color.FromArgb(0x{{color.A:X2}}, 0x{{color.R:X2}}, 0x{{color.G:X2}}, 0x{{color.B:X2}});
                        """
                    );
                }
                else
                {
                    writer.WriteLine($$"""
                        {{indent}}{{indent}}{{propertyPrefix}}{{brush.PropertyName}} = BaseThemeColors.{{value}};
                        """);
                }
            }

            foreach (TreeItem<Brush> child in treeItem.Children)
            {
                WriteTreeItem(child, writer, theme, $"{propertyPrefix}{child.Name.Pluralize()}.");
            }
        }
    }

    private static void GenerateResourceDictionaryExtensions(TreeItem<Brush> brushes, DirectoryInfo repoRoot)
    {
        using var writer = new StreamWriter(Path.Combine(repoRoot.FullName, "MaterialDesignThemes.Wpf", "ResourceDictionaryExtensions.g.cs"));

        string indent = new(' ', CSharpIndentSize);
        writer.WriteLine(AutoGeneratedHeader);

        writer.WriteLine($$"""
            namespace MaterialDesignThemes.Wpf;
            static partial class ResourceDictionaryExtensions
            {
            """);

        writer.WriteLine($$"""
            {{indent}}private static partial void LoadThemeColors(ResourceDictionary resourceDictionary, Theme theme)
            {{indent}}{
            """);
        LoadThemeColors(brushes, writer, 2, "theme.");
        writer.WriteLine($"{indent}}}");

        writer.WriteLine();

        writer.WriteLine($$"""
            {{indent}}private static partial void ApplyThemeColors(ResourceDictionary resourceDictionary, Theme theme)
            {{indent}}{
            """);
        ApplyThemeColors(brushes, writer, 2, "theme.");
        writer.WriteLine($"{indent}}}");


        writer.WriteLine("}");

        static void LoadThemeColors(TreeItem<Brush> treeItem, StreamWriter writer, int indentLevel, string propertyPrefix)
        {
            string indent = new(' ', indentLevel * CSharpIndentSize);

            foreach (Brush brush in treeItem.Values)
            {
                string keys = string.Join("\", \"", GetResourceKeys());
                writer.WriteLine($"{indent}{propertyPrefix}{brush.PropertyName} = GetColor(resourceDictionary, \"{keys}\");");

                IEnumerable<string> GetResourceKeys()
                {
                    if (!string.IsNullOrWhiteSpace(brush.Name))
                    {
                        yield return brush.Name;
                    }
                    foreach (string key in brush.AlternateKeys ?? Enumerable.Empty<string>())
                    {
                        yield return key;
                    }
                    foreach (string key in brush.ObsoleteKeys ?? Enumerable.Empty<string>())
                    {
                        yield return key;
                    }
                }
            }

            foreach (TreeItem<Brush> child in treeItem.Children)
            {
                LoadThemeColors(child, writer, indentLevel, $"{propertyPrefix}{child.Name.Pluralize()}.");
            }
        }

        static void ApplyThemeColors(TreeItem<Brush> treeItem, StreamWriter writer, int indentLevel, string propertyPrefix)
        {
            string indent = new(' ', indentLevel * CSharpIndentSize);

            foreach (Brush brush in treeItem.Values)
            {
                foreach (var key in GetResourceKeys())
                {
                    writer.WriteLine($"{indent}SetSolidColorBrush(resourceDictionary, \"{key}\", {propertyPrefix}{brush.PropertyName});");
                }

                IEnumerable<string> GetResourceKeys()
                {
                    if (!string.IsNullOrWhiteSpace(brush.Name))
                    {
                        yield return brush.Name;
                    }
                    foreach (string key in brush.AlternateKeys ?? Enumerable.Empty<string>())
                    {
                        yield return key;
                    }
                    //TODO: Conditionally include this
                    foreach (string key in brush.ObsoleteKeys ?? Enumerable.Empty<string>())
                    {
                        yield return key;
                    }
                }
            }

            foreach (TreeItem<Brush> child in treeItem.Children)
            {
                ApplyThemeColors(child, writer, indentLevel, $"{propertyPrefix}{child.Name.Pluralize()}.");
            }
        }

    }

    private static void GenerateThemeBrushTests(IEnumerable<Brush> brushes, DirectoryInfo repoRoot)
    {
        string indent = new(' ', CSharpIndentSize);
        string xamlIndent = new(' ', XamlIndentSize);
        using var writer = new StreamWriter(Path.Combine(repoRoot.FullName, "MaterialDesignThemes.UITests", "WPF", "Theme", "ThemeTests.g.cs"));
        writer.WriteLine($$"""
                {{AutoGeneratedHeader}}
                using System.Windows.Media;

                namespace MaterialDesignThemes.UITests.WPF.Theme;

                partial class ThemeTests
                {
                """);
        WriteGetXamlWrapPanel();

        WriteAssertAllThemeBrushesSet();

        WriteBrushNames();

        writer.WriteLine("}");

        void WriteGetXamlWrapPanel()
        {
            writer.WriteLine($$""""
                {{indent}}private partial string GetXamlWrapPanel()
                {{indent}}{
                {{indent}}{{indent}}return """
                {{indent}}{{indent}}<WrapPanel>
                {{indent}}{{indent}}{{xamlIndent}}<WrapPanel.Resources>
                {{indent}}{{indent}}{{xamlIndent}}{{xamlIndent}}<Style TargetType="TextBlock">
                {{indent}}{{indent}}{{xamlIndent}}{{xamlIndent}}{{xamlIndent}}<Setter Property="Height" Value="50"/>
                {{indent}}{{indent}}{{xamlIndent}}{{xamlIndent}}{{xamlIndent}}<Setter Property="Width" Value="50"/>
                {{indent}}{{indent}}{{xamlIndent}}{{xamlIndent}}</Style>
                {{indent}}{{indent}}{{xamlIndent}}</WrapPanel.Resources>
                """");
            foreach (Brush brush in brushes)
            {
                WriteBrush(brush.Name!, brush.NameWithoutPrefix);
            }
            foreach (Brush brush in GetAllObsoleteBrushes(brushes))
            {
                WriteBrush(brush.Name!, brush.Name!);
            }
            foreach (string primaryColor in PrimaryColorBrushNames())
            {
                WriteBrush(primaryColor, primaryColor);
            }
            foreach (string secondaryColor in SecondaryColorBrushNames())
            {
                WriteBrush(secondaryColor, secondaryColor);
            }

            writer.WriteLine($$""""
                {{indent}}{{indent}}</WrapPanel>
                {{indent}}{{indent}}""";
                {{indent}}}
                """");

            void WriteBrush(string brushName, string displayName)
            {
                writer.WriteLine($$"""
                {{indent}}{{indent}}{{xamlIndent}}<TextBlock Text="{{displayName}}" Background="{StaticResource {{brushName}}}" />
                """);
            }
        }

        void WriteAssertAllThemeBrushesSet()
        {
            writer.WriteLine($$"""
                {{indent}}private partial async Task AssertAllThemeBrushesSet(IVisualElement<WrapPanel> panel)
                {{indent}}{
                """);
            foreach (Brush brush in brushes)
            {
                WriteBrush(brush, brush.NameWithoutPrefix);
            }
            foreach (Brush brush in GetAllObsoleteBrushes(brushes))
            {
                WriteBrush(brush, brush.Name!);
            }
            writer.WriteLine($$"""
                {{indent}}}
                """);

            void WriteBrush(Brush brush, string name)
            {
                writer.WriteLine($$"""
                    {{indent}}{{indent}}{
                    {{indent}}{{indent}}{{indent}}IVisualElement<TextBlock> textBlock = await panel.GetElement<TextBlock>("[Text=\"{{name}}\"]");
                    {{indent}}{{indent}}{{indent}}Color? textBlockBackground = await textBlock.GetBackgroundColor();
                    {{indent}}{{indent}}{{indent}}Assert.Equal(await GetResourceColor("{{brush.Name}}"), textBlockBackground);
                    {{indent}}{{indent}}}
                    """);
            }
        }

        void WriteBrushNames()
        {
            writer.WriteLine($$"""
                {{indent}}private static IEnumerable<string> GetBrushResourceNames()
                {{indent}}{
                """);
            foreach (Brush brush in brushes)
            {
                writer.WriteLine($$"""
                    {{indent}}{{indent}}yield return "{{brush.Name}}";
                    """);
            }
            writer.WriteLine($$"""
                {{indent}}}
                """);

            writer.WriteLine($$"""
                {{indent}}private static IEnumerable<string> GetObsoleteBrushResourceNames()
                {{indent}}{
                """);
            foreach (Brush brush in GetAllObsoleteBrushes(brushes))
            {
                writer.WriteLine($$"""
                    {{indent}}{{indent}}yield return "{{brush.Name}}";
                    """);
            }
            writer.WriteLine($$"""
                {{indent}}}
                """);
        }
    }

    private static IEnumerable<string> PrimaryColorBrushNames()
    {
        yield return "MaterialDesign.Brush.Primary.Light";
        yield return "MaterialDesign.Brush.Primary.Light.Foreground";
        yield return "MaterialDesign.Brush.Primary";
        yield return "MaterialDesign.Brush.Primary.Foreground";
        yield return "MaterialDesign.Brush.Primary.Dark";
        yield return "MaterialDesign.Brush.Primary.Dark.Foreground";
    }

    private static IEnumerable<string> SecondaryColorBrushNames()
    {
        yield return "MaterialDesign.Brush.Secondary.Light";
        yield return "MaterialDesign.Brush.Secondary.Light.Foreground";
        yield return "MaterialDesign.Brush.Secondary";
        yield return "MaterialDesign.Brush.Secondary.Foreground";
        yield return "MaterialDesign.Brush.Secondary.Dark";
        yield return "MaterialDesign.Brush.Secondary.Dark.Foreground";
    }

    private static DirectoryInfo? GetRepoRoot()
    {
        DirectoryInfo? currentDirectory = new(Environment.CurrentDirectory);
        while (currentDirectory is not null && !currentDirectory.EnumerateDirectories(".git").Any())
        {
            currentDirectory = currentDirectory.Parent;
        }
        return currentDirectory;
    }

    private static TreeItem<Brush> BuildBrushTree(IEnumerable<Brush> brushes)
    {
        TreeItem<Brush> root = new("");

        foreach (Brush brush in brushes)
        {
            TreeItem<Brush> current = root;
            foreach (string part in brush.ContainerParts)
            {
                TreeItem<Brush>? child = current.Children.FirstOrDefault(x => x.Name == part);
                if (child is null)
                {
                    child = new(part);
                    current.Children.Add(child);
                }
                current = child;
            }
            current.Values.Add(brush);
        }

        return root;
    }

    private static IEnumerable<Brush> GetAllAlternateBrushesFlattened(IEnumerable<Brush> brushes)
    {
        return brushes.SelectMany(GetAllAlternateBrushes);

        static IEnumerable<Brush> GetAllAlternateBrushes(Brush x)
        {
            yield return x with
            {
                AlternateKeys = null
            };
            foreach (string key in x.AlternateKeys ?? Enumerable.Empty<string>())
            {
                yield return x with
                {
                    Name = key,
                    AlternateKeys = null,
                };
            }
        }
    }

    private static IEnumerable<Brush> GetAllObsoleteBrushes(IEnumerable<Brush> brushes)
    {
        return brushes.SelectMany(GetAllObsoleteBrushes);

        static IEnumerable<Brush> GetAllObsoleteBrushes(Brush x)
        {
            foreach (string key in x.ObsoleteKeys ?? Enumerable.Empty<string>())
            {
                yield return x with
                {
                    Name = key,
                    AlternateKeys = null,
                    ObsoleteKeys = null,
                };
            }
        }
    }
}

public record class Brush(
    [property:JsonPropertyName("name")]
    string? Name,
    [property:JsonPropertyName("themeValues")]
    ThemeValues ThemeValues,
    [property:JsonPropertyName("alternateKeys")]
    string[]? AlternateKeys,
    [property:JsonPropertyName("obsoleteKeys")]
    string[]? ObsoleteKeys)
{
    public const string BrushPrefix = "MaterialDesign.Brush.";
    public string PropertyName => Name!.Split(".")[^1];
    public string NameWithoutPrefix => Name![BrushPrefix.Length..];
    public string[] ContainerParts => Name!.Split('.')[2..^1];
    public string ContainerTypeName => string.Join('.', ContainerParts);
}

public record class ThemeValues(
    [property:JsonPropertyName("light")]
    string Light,
    [property:JsonPropertyName("dark")]
    string Dark)
{
    public string this[string theme]
    {
        get
        {
            return theme.ToLowerInvariant() switch
            {
                "light" => Light,
                "dark" => Dark,
                _ => throw new InvalidOperationException($"Unknown theme: {theme}")
            };
        }
    }
}

