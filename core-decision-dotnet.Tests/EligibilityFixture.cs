using System.Data.SQLite;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace Photon.JobSeeker.Tests;

internal sealed class EligibilityFixture : IDisposable
{
    private static readonly Regex plain_word = new("^[a-z]{3,}$");

    private readonly Dictionaries dictionaries;

    public JobEligibilityHelper Helper { get; }

    public EligibilityFixture(string[]? englishWords = null, params JobOption[] options)
    {
        dictionaries = CreateDictionaries(englishWords ?? []);
        Helper = new JobEligibilityHelper(dictionaries, CreateDatabase(), options);
    }

    public void Dispose()
    {
        Helper.Dispose();
        dictionaries.Dispose();
    }

    public static Dictionaries CreateDictionaries(params string[] englishWords)
    {
        var connection = new SQLiteConnection("Data Source=:memory:");
        connection.Open();

        var executer = connection.CreateCommand();
        executer.CommandText = "PRAGMA busy_timeout=5000";
        executer.ExecuteNonQuery();
        executer.CommandText = "CREATE TABLE en_US (Word TEXT)";
        executer.ExecuteNonQuery();
        executer.CommandText = "INSERT INTO en_US (Word) VALUES ($word)";

        var word_parameter = executer.CreateParameter();
        word_parameter.ParameterName = "$word";
        executer.Parameters.Add(word_parameter);

        foreach (var word in englishWords.Distinct())
        {
            if (!plain_word.IsMatch(word))
                throw new ArgumentException($"Seeded word must be plain [a-z]{{3,}}: '{word}'");

            word_parameter.Value = word;
            executer.ExecuteNonQuery();
        }

        executer.Parameters.Clear();

        return new Dictionaries(connection, executer);
    }

    public static Database CreateDatabase()
    {
        var connection = new SQLiteConnection("Data Source=:memory:");
        connection.Open();
        return new Database(connection, connection.CreateCommand());
    }

    public static JobOption Option(string category, long score, string pattern,
                                   string? title = null, object? settings = null)
    {
        return new JobOption
        {
            Category = category,
            Score = score,
            Title = title ?? category,
            Pattern = new Regex(pattern, RegexOptions.IgnoreCase),
            Settings = settings,
        };
    }

    public static JobOption SalaryOption(long score, int moneyGroup, int periodGroup, string pattern)
    {
        return new JobOption
        {
            Category = "salary",
            Score = score,
            Title = "salary",
            Pattern = new Regex(pattern),
            Settings = JsonConvert.DeserializeObject<dynamic>(
                $"{{ \"money\": {moneyGroup}, \"period\": {periodGroup} }}"),
        };
    }

    public static Job MakeJob(string? content, string? title = null)
    {
        return new Job { Content = content, Title = title };
    }

    public static string[] GenerateWords(int count)
    {
        var words = new string[count];

        for (var i = 0; i < count; i++)
        {
            var letters = new char[2];
            var n = i;
            for (var p = letters.Length - 1; p >= 0; p--)
            {
                letters[p] = (char)('a' + n % 26);
                n /= 26;
            }
            words[i] = "w" + new string(letters);
        }

        return words;
    }
}
