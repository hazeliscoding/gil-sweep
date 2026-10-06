namespace GilSweep.Core.Catalog;

/// <summary>One recipe from the bundled crafts.json.</summary>
public sealed class CraftRecipe
{
    public int Id { get; set; }

    public string Name { get; set; } = "";

    /// <summary>Crafter job abbreviation, or "any".</summary>
    public string Job { get; set; } = "";

    public int? Rlvl { get; set; }

    public int? Lvl { get; set; }

    public int Yield { get; set; } = 1;

    public List<RecipeIngredient> Ingredients { get; set; } = [];
}

public sealed class RecipeIngredient
{
    public int Id { get; set; }

    public string Name { get; set; } = "";

    public int Qty { get; set; }
}

/// <summary>
/// Demand signals for one tracked item from the bundled garland-demand.json, extracted from
/// Garland Tools: recipes that consume it, leves, Grand Company supply missions, quests.
/// </summary>
public sealed class DemandSignals
{
    public int V { get; set; }

    public int Id { get; set; }

    public int RecipeCount { get; set; }

    public List<RecipeIngredient> Consumers { get; set; } = [];

    public int Leves { get; set; }

    public DemandSupply? Supply { get; set; }

    public int Quests { get; set; }
}

public sealed class DemandSupply
{
    public int Count { get; set; }

    public int Seals { get; set; }
}
