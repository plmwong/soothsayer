using NUnit.Framework;
using soothsayer.Commands;

namespace soothsayer.Tests.Commands
{
    [TestFixture]
    public class OptionsFileTests
    {
        [Test]
        public void when_the_options_file_is_not_present_then_there_are_no_overrides()
        {
            var optionsFile = new OptionsFile("thisfiledoesnotexist.json");

            Assert.That(optionsFile.IsEmpty());
        }

        [Test]
        public void when_an_option_is_configured_in_file_then_it_will_override_an_empty_value()
        {
            var optionsFile = new OptionsFile("Commands\\changeusername.json");
            var testDatabaseCommandOptions = new TestDatabaseCommandOptions();

            Assert.That(testDatabaseCommandOptions.Username, Is.Null);

            var overriddenArguments = optionsFile.ApplyTo(testDatabaseCommandOptions);

            Assert.That(testDatabaseCommandOptions.Username, Is.EqualTo("some other username"));
        }

        [Test]
        public void when_an_option_is_configured_in_file_then_it_will_override_the_option_value_when_applied()
        {
            var optionsFile = new OptionsFile("Commands\\changeusername.json");
            var testDatabaseCommandOptions = new TestDatabaseCommandOptions { Username = "existing username" };

            optionsFile.ApplyTo(testDatabaseCommandOptions);

            Assert.That(testDatabaseCommandOptions.Username, Is.EqualTo("some other username"));
        }

        [Test]
        public void a_configured_option_which_does_not_match_any_existing_option_will_have_no_effect()
        {
            var optionsFile = new OptionsFile("Commands\\changesomething.json");
            var testDatabaseCommandOptions = new TestDatabaseCommandOptions { Username = "existing username" };

            optionsFile.ApplyTo(testDatabaseCommandOptions);

            Assert.That(testDatabaseCommandOptions.Username, Is.EqualTo("existing username"));
        }
    }
}