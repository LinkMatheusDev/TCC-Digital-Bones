using SQLite;

[Table("AnatomyModels")]
public class AnatomyModelRecord
{
    [PrimaryKey]
    public string ModelId { get; set; }

    [NotNull]
    public string DisplayName { get; set; }

    public string Species { get; set; }

    public string Bone { get; set; }

    public string Description { get; set; }

    public string PrefabKey { get; set; }
}