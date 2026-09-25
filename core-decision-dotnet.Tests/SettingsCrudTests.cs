namespace Photon.JobSeeker.Tests;

public class SettingsCrudTests
{
    [Fact]
    public void AppSetting_save_inserts_then_updates()
    {
        using var db = new GoldenDatabase();
        var settings = db.Database.AppSetting;

        settings.Save(AppSettingBusiness.FloorKey, "85");
        Assert.Equal(85, settings.Floor());

        settings.Save(AppSettingBusiness.FloorKey, "90");
        Assert.Equal(90, settings.Floor());
    }

    [Fact]
    public void AppSetting_save_rejects_unknown_key()
    {
        using var db = new GoldenDatabase();

        Assert.Throws<BadJobRequest>(() => db.Database.AppSetting.Save("nope", "1"));
    }

    [Theory]
    [InlineData(AppSettingBusiness.FloorKey, "abc")]
    [InlineData(AppSettingBusiness.WRegexKey, "abc")]
    [InlineData(AppSettingBusiness.WAiKey, "")]
    public void AppSetting_save_rejects_non_numeric_value(string key, string value)
    {
        using var db = new GoldenDatabase();

        Assert.Throws<BadJobRequest>(() => db.Database.AppSetting.Save(key, value));
    }

    [Fact]
    public void AppSetting_fetch_all_returns_saved_rows()
    {
        using var db = new GoldenDatabase();
        var settings = db.Database.AppSetting;

        settings.Save(AppSettingBusiness.FloorKey, "75");
        settings.Save(AppSettingBusiness.WRegexKey, "0.4");

        var values = settings.FetchAll();
        Assert.Equal("75", values[AppSettingBusiness.FloorKey]);
        Assert.Equal("0.4", values[AppSettingBusiness.WRegexKey]);
    }

    [Fact]
    public void Option_save_inserts_and_fetch_rows_sees_it()
    {
        using var db = new GoldenDatabase();
        var options = db.Database.JobOption;

        var id = options.Save(new OptionEditRow
        {
            Category = "field",
            Score = 40,
            Title = "Python",
            Pattern = @"\bpython\b",
        });

        Assert.True(id > 0);

        var row = options.FetchRows().Single(r => r.JobOptionID == id);
        Assert.Equal("field", row.Category);
        Assert.Equal(40, row.Score);
        Assert.Equal("Python", row.Title);
        Assert.True(row.Efective);
        Assert.Null(row.Settings);
    }

    [Fact]
    public void Option_save_updates_existing_row()
    {
        using var db = new GoldenDatabase();
        var options = db.Database.JobOption;

        var id = options.Save(new OptionEditRow
        {
            Category = "tech",
            Score = 10,
            Title = "Git",
            Pattern = @"\bgit(hub|lab)?\b",
        });

        options.Save(new OptionEditRow
        {
            JobOptionID = id,
            Category = "tech",
            Score = 20,
            Title = "Git",
            Pattern = @"\bgit(hub|lab)?\b",
            Settings = @"{ ""resume"": null }",
        });

        var row = options.FetchRows().Single(r => r.JobOptionID == id);
        Assert.Equal(20, row.Score);
        Assert.Equal(@"{ ""resume"": null }", row.Settings);
        Assert.Contains(options.FetchAll(), o => o.Title == "Git" && o.Score == 20);
    }

    [Fact]
    public void Option_save_rejects_missing_id()
    {
        using var db = new GoldenDatabase();

        Assert.Throws<BadJobRequest>(() => db.Database.JobOption.Save(new OptionEditRow
        {
            JobOptionID = 99,
            Category = "tech",
            Score = 1,
            Title = "Missing",
            Pattern = "x",
        }));
    }

    [Fact]
    public void Option_save_rejects_duplicate_title()
    {
        using var db = new GoldenDatabase();
        var options = db.Database.JobOption;

        options.Save(new OptionEditRow { Category = "tech", Score = 1, Title = "Git", Pattern = "git" });

        Assert.Throws<BadJobRequest>(() => options.Save(new OptionEditRow
        {
            Category = "field",
            Score = 2,
            Title = "Git",
            Pattern = "git",
        }));
    }

    [Fact]
    public void Option_save_rejects_invalid_pattern()
    {
        using var db = new GoldenDatabase();

        Assert.Throws<BadJobRequest>(() => db.Database.JobOption.Save(new OptionEditRow
        {
            Category = "tech",
            Score = 1,
            Title = "Broken",
            Pattern = "(unclosed",
        }));
    }

    [Theory]
    [InlineData("{ nope")]
    [InlineData("[1,2]")]
    public void Option_save_rejects_invalid_settings_json(string settings)
    {
        using var db = new GoldenDatabase();

        Assert.Throws<BadJobRequest>(() => db.Database.JobOption.Save(new OptionEditRow
        {
            Category = "tech",
            Score = 1,
            Title = "BadJson",
            Pattern = "x",
            Settings = settings,
        }));
    }

    [Fact]
    public void Option_blank_settings_is_stored_as_null()
    {
        using var db = new GoldenDatabase();
        var options = db.Database.JobOption;

        var id = options.Save(new OptionEditRow
        {
            Category = "tech",
            Score = 1,
            Title = "Blank",
            Pattern = "x",
            Settings = "   ",
        });

        Assert.Null(options.FetchRows().Single(r => r.JobOptionID == id).Settings);
    }

    [Fact]
    public void Option_set_effective_excludes_row_from_scoring()
    {
        using var db = new GoldenDatabase();
        var options = db.Database.JobOption;

        var id = options.Save(new OptionEditRow
        {
            Category = "field",
            Score = 80,
            Title = "Java",
            Pattern = @"\bjava\b",
        });

        Assert.Contains(options.FetchAll(), o => o.Title == "Java");

        Assert.True(options.SetEffective(id, false));
        Assert.DoesNotContain(options.FetchAll(), o => o.Title == "Java");

        var row = options.FetchRows().Single(r => r.JobOptionID == id);
        Assert.False(row.Efective);

        Assert.True(options.SetEffective(id, true));
        Assert.Contains(options.FetchAll(), o => o.Title == "Java");
    }

    [Fact]
    public void Option_delete_removes_row()
    {
        using var db = new GoldenDatabase();
        var options = db.Database.JobOption;

        var id = options.Save(new OptionEditRow
        {
            Category = "field",
            Score = 80,
            Title = "Java",
            Pattern = @"\bjava\b",
        });

        Assert.True(options.Delete(id));
        Assert.DoesNotContain(options.FetchRows(), r => r.JobOptionID == id);
        Assert.False(options.Delete(id));
    }
}
