namespace Dracocephalum.Fortrania.Plugins;

/// <summary>
/// Code of a user's own that takes part in a translation: each method is
/// called at one point of every run, before or after a stage, with the run's
/// context. A plugin implements the methods it needs; the others do nothing.
/// </summary>
/// <remarks>
/// <para>
/// The plugins of a run are called in the <see cref="Order"/> they give,
/// one after another, never at once, and each stage goes on only where none of them
/// reported an error through <see cref="TranslationContext.Error"/>. A
/// plugin that throws stops the run with an error that names it and the
/// stage: an exception says nothing a message could not. What a plugin may
/// change at each point is said on its method; everything else in the
/// context is to be read.
/// </para>
/// <para>
/// The translator makes the plugins of each assembly it is told of, by the
/// settings' <see cref="Translation.TranslationSettings.Plugins"/> or the
/// options' <see cref="Translation.TranslationOptions.PluginAssemblies"/>,
/// as every public type there that implements this and has a constructor
/// that takes nothing. A program that translates through
/// <see cref="Translation.Translator"/> may pass its own in
/// <see cref="Translation.TranslationOptions.Plugins"/>.
/// </para>
/// </remarks>
public interface ITranslationPlugin
{
    /// <summary>
    /// Gets where the plugin is called among the others of a run: one of a
    /// lower order before one of a higher, at every point, and two of the
    /// same order in the order they were given. Zero unless it says.
    /// </summary>
    int Order => 0;

    /// <summary>Called once the settings are checked and the sources fetched, before a source is read.</summary>
    /// <param name="context">The run.</param>
    void BeforeRead(TranslationContext context)
    {
    }

    /// <summary>Called once every source is read.</summary>
    /// <param name="context">The run.</param>
    void AfterRead(TranslationContext context)
    {
    }

    /// <summary>Called before the calls of the sources read are checked against what they call, and their effects worked out.</summary>
    /// <param name="context">The run.</param>
    void BeforeAnalysis(TranslationContext context)
    {
    }

    /// <summary>Called once every call is checked and every procedure's effects and entries are worked out.</summary>
    /// <param name="context">The run.</param>
    void AfterAnalysis(TranslationContext context)
    {
    }

    /// <summary>
    /// Called once every file of the translation is made and before any is
    /// written. Each of <see cref="TranslationContext.GeneratedFiles"/> may
    /// be given another text, which is what is then written and what tells
    /// the next run whether the file changed.
    /// </summary>
    /// <param name="context">The run.</param>
    void BeforeWrite(TranslationContext context)
    {
    }

    /// <summary>
    /// Called once the translation is written, with what it did in
    /// <see cref="TranslationContext.Result"/>, before the settings' own
    /// commands run.
    /// </summary>
    /// <param name="context">The run.</param>
    void AfterWrite(TranslationContext context)
    {
    }
}
