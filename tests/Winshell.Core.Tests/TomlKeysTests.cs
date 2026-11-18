namespace Winshell.Core.Tests;

[TestClass]
public sealed class TomlKeysTests
{
    private readonly List<string> _warnings = [];

    private sealed class Settings
    {
        public string Name { get; set; } = "default";

        public bool On { get; set; } = true;

        public int Size { get; set; } = 3;

        public double Scale { get; set; } = 1.5;

        public IReadOnlyList<string> Tiles { get; set; } = ["wifi"];

        public IReadOnlyList<IReadOnlyDictionary<string, object?>> Hidden { get; set; } = [];

        public long Number { get; set; }

        public int? Maybe { get; set; }

        public object? Anything { get; set; }

        public IReadOnlyDictionary<string, object?>? Taskbar { get; set; }

        public IReadOnlyList<string>? Pins { get; set; }
    }

    private Settings Load(string text, Func<string, object, string>? check = null)
    {
        Assert.IsTrue(TomlDocument.TryParse("settings.toml", text, out var document, out var error), error);
        var settings = new Settings();
        var keys = new TomlKeys(settings, check, _warnings.Add);
        keys.Apply(document.Values, document.LocationOf, null,
            keys.All.ToDictionary(key => key, key => key.Value(settings)));
        return settings;
    }

    private static string BackdropCheck(string key, object value)
    {
        return key == "name" && value is not ("acrylic" or "mica") ? "is not \"acrylic\" or \"mica\"" : "";
    }

    [TestMethod]
    public void Values_are_read_into_their_properties_and_a_key_the_file_does_not_have_keeps_its_default()
    {
        var settings = Load("""
                            name = 'from "file"'
                            size = 7
                            scale = 2
                            hidden = [{ id = "Contoso.Chat", name = "Chat" }, { id = "Contoso.Mail" }]
                            unknown = 1
                            """);

        Assert.AreEqual("from \"file\"", settings.Name);
        Assert.AreEqual(7, settings.Size);
        Assert.AreEqual(2.0, settings.Scale);
        Assert.IsTrue(settings.On);
        Assert.AreSequenceEqual(["Contoso.Chat", "Contoso.Mail"], settings.Hidden.Select(row => row["id"]));
        Assert.AreEqual("Chat", settings.Hidden[0]["name"]);
        Assert.IsFalse(settings.Hidden[1].ContainsKey("name"));
        Assert.IsEmpty(_warnings);
    }

    [TestMethod]
    public void A_nullable_property_holds_null_until_the_file_has_its_key_and_an_empty_list_is_not_null()
    {
        var none = Load("");
        Assert.IsNull(none.Maybe);
        Assert.IsNull(none.Anything);
        Assert.IsNull(none.Taskbar);
        Assert.IsNull(none.Pins);

        var settings = Load("maybe = 2\nanything = \"text\"\ntaskbar.centered = true\npins = []\n");

        Assert.AreEqual(2, settings.Maybe);
        Assert.AreEqual("text", settings.Anything);
        Assert.AreEqual(true, settings.Taskbar!["centered"]);
        Assert.IsNotNull(settings.Pins);
        Assert.IsEmpty(settings.Pins);
        Assert.IsEmpty(_warnings);
    }

    [TestMethod]
    [DataRow("on = \"false\"", "[1:1]: on should be true or false, not \"false\"")]
    [DataRow("on = [false]", "[1:1]: on should be true or false, not [false]")]
    [DataRow("  on = { on = false }", "[1:3]: on should be true or false, not { on = false }")]
    [DataRow("name = 0x0078D4", "[1:1]: name should be a string, not 30932")]
    [DataRow("size = \"x\"", "[1:1]: size should be a whole number from -2147483648 to 2147483647, not \"x\"")]
    [DataRow("size = 3000000000", "[1:1]: size should be a whole number from -2147483648 to 2147483647, not 3000000000")]
    [DataRow("tiles = [\"wifi\", 2]", "[1:1]: tiles should be a list of strings, not [\"wifi\", 2]")]
    public void A_value_its_property_cannot_hold_is_left_and_named_with_its_line(string line, string warning)
    {
        var settings = Load(line + "\nscale = 2\n");

        var defaults = new Settings();
        Assert.AreSequenceEqual([$"settings.toml{warning}, so it was left unchanged"], _warnings);
        Assert.AreEqual((defaults.Name, defaults.On, defaults.Size), (settings.Name, settings.On, settings.Size));
        Assert.AreSequenceEqual(defaults.Tiles, settings.Tiles);
        Assert.AreEqual(2.0, settings.Scale);
    }

    [TestMethod]
    [DataRow("0", false)]
    [DataRow("1", true)]
    [DataRow("0.0", false)]
    [DataRow("0.5", true)]
    [DataRow("nan", false)]
    public void A_bool_property_takes_a_number(string value, bool expected)
    {
        var settings = Load($"on = {value}\n");

        Assert.AreEqual(expected, settings.On);
        Assert.IsEmpty(_warnings);
    }

    [TestMethod]
    public void A_long_property_holds_an_integer_past_2_to_the_53_exactly()
    {
        var settings = Load("number = 9223372036854775807\n");

        Assert.AreEqual(long.MaxValue, settings.Number);
    }

    [TestMethod]
    [DataRow("\"Mica\"", "default", true)]
    [DataRow("\"mica\"", "mica", false)]
    public void The_check_refuses_a_value_by_name_and_takes_one_it_allows(string value, string expected,
        bool refused)
    {
        var settings = Load($"name = {value}\nsize = 4\n", BackdropCheck);

        Assert.AreEqual(expected, settings.Name);
        Assert.AreEqual(4, settings.Size);
        Assert.AreSequenceEqual(refused ? ["settings.toml[1:1]: name is not \"acrylic\" or \"mica\", so it was left unchanged"] : [],
            _warnings);
    }

    [TestMethod]
    public void A_key_that_looks_like_one_it_reads_is_named_with_its_line()
    {
        var settings = Load("name = \"kept\"\nSize = 4\n  tilez = [\"wifi\"]\nin = 1\n\n[colors]\nmode = \"dark\"\n");

        Assert.AreSequenceEqual(
            [
                "settings.toml[2:1]: Size was ignored; did you mean size?",
                "settings.toml[3:3]: tilez was ignored; did you mean tiles?"
            ],
            _warnings);
        Assert.AreEqual(3, settings.Size);
    }
}
