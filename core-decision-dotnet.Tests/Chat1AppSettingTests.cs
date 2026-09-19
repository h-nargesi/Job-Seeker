namespace Photon.JobSeeker.Tests;

public class Chat1AppSettingTests
{
    [Fact]
    public void Typed_getters_use_defaults_when_keys_are_absent()
    {
        using var db = new GoldenDatabase();
        var settings = db.Database.AppSetting;

        Assert.Equal(AppSettingBusiness.FloorDefault, settings.Floor());
        Assert.Equal(AppSettingBusiness.AiPassmarkDefault, settings.AiPassmark());
        Assert.Equal(AppSettingBusiness.ScoreCapDefault, settings.ScoreCap());
        Assert.Equal(AppSettingBusiness.WRegexDefault, settings.WRegex(), 10);
        Assert.Equal(AppSettingBusiness.WAiDefault, settings.WAi(), 10);
    }

    [Fact]
    public void Typed_getters_read_seed_values()
    {
        using var db = new GoldenDatabase();
        SeedDefaults(db);

        var settings = db.Database.AppSetting;
        Assert.Equal(70, settings.Floor());
        Assert.Equal(60, settings.AiPassmark());
        Assert.Equal(300, settings.ScoreCap());
        Assert.Equal(0.35, settings.WRegex(), 10);
        Assert.Equal(0.65, settings.WAi(), 10);
    }

    [Fact]
    public void Typed_getters_are_uncached()
    {
        using var db = new GoldenDatabase();
        db.ExecuteRaw("INSERT INTO AppSetting (Key, Value) VALUES ('floor', '70')");

        Assert.Equal(70, db.Database.AppSetting.Floor());

        db.ExecuteRaw("UPDATE AppSetting SET Value = '85' WHERE Key = 'floor'");

        Assert.Equal(85, db.Database.AppSetting.Floor());
    }

    [Fact]
    public void Typed_getters_fall_back_when_the_stored_value_is_not_numeric()
    {
        using var db = new GoldenDatabase();
        db.ExecuteRaw("INSERT INTO AppSetting (Key, Value) VALUES ('floor', 'nope'), ('w_regex', 'bad')");

        Assert.Equal(AppSettingBusiness.FloorDefault, db.Database.AppSetting.Floor());
        Assert.Equal(AppSettingBusiness.WRegexDefault, db.Database.AppSetting.WRegex(), 10);
    }

    [Fact]
    public void Phase1_keys_are_not_job_option_settings()
    {
        var names = typeof(JobOptionSettings).GetProperties().Select(p => p.Name).ToHashSet();

        Assert.DoesNotContain("Floor", names);
        Assert.DoesNotContain("AiPassmark", names);
        Assert.DoesNotContain("ScoreCap", names);
        Assert.DoesNotContain("WRegex", names);
        Assert.DoesNotContain("WAi", names);
    }

    private static void SeedDefaults(GoldenDatabase db)
    {
        db.ExecuteRaw(@"
INSERT INTO AppSetting (Key, Value) VALUES
    ('floor', '70'),
    ('aipassmark', '60'),
    ('scorecap', '300'),
    ('w_regex', '0.35'),
    ('w_ai', '0.65')");
    }
}
