using Dracocephalum.Fortrania.Translation;

namespace Dracocephalum.Fortrania.Tests.Translation;

/// <summary>What the translator's tests reach of a project they made.</summary>
internal static class TranslationProjectTestExtensions
{
    extension(TranslationProject project)
    {
        /// <summary>Gets the settings of a project made of one settings file, as most tests' are.</summary>
        public TranslationSettings Part => project.Settings[0];
    }
}
