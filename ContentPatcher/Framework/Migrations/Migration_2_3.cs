using System.Diagnostics.CodeAnalysis;
using StardewModdingAPI;

namespace ContentPatcher.Framework.Migrations;

/// <summary>Migrates patches to format version 2.2.</summary>
[SuppressMessage("ReSharper", "InconsistentNaming", Justification = "Named for clarity.")]
internal partial class Migration_2_3 : BaseMigration
{
    /*********
    ** Public methods
    *********/
    /// <summary>Construct an instance.</summary>
    public Migration_2_3()
        : base(new SemanticVersion(2, 3, 0)) { }
}
