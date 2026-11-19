using Dracocephalum.Spice.Translation;

// The SPICE Toolkit's translation: the settings beside this project, and
// what is done to the translation after Fortrania has written it.
//
//   translate [--force]   translates the toolkit as the settings say; the
//                         settings run "rewrite" afterwards
//   rewrite               rewrites the files a translation wrote, which it
//                         finds listed in FORTRANIA_WRITTEN
return args switch
{
    ["translate", .. var rest] => Commands.Translate(rest.Contains("--force", StringComparer.Ordinal)),
    ["rewrite"] => Commands.Rewrite(),
    _ => Commands.ShowUsage(),
};
