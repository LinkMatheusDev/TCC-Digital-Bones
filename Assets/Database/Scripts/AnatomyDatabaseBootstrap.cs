
using UnityEngine;

public class AnatomyDatabaseBootstrap : MonoBehaviour
{
    [SerializeField]
    private AnatomyModelDefinition[] catalog;

    private void Start()
    {
        AnatomyDatabase.Initialize();

        if (catalog == null || catalog.Length == 0)
        {
            Debug.LogError("Nenhum modelo foi associado ao catálogo!");
            return;
        }

        int registered = 0;

        foreach (AnatomyModelDefinition definition in catalog)
        {
            if (definition == null)
                continue;

            if (string.IsNullOrWhiteSpace(definition.ModelId))
            {
                Debug.LogWarning("Modelo sem identificador. Ignorado.");
                continue;
            }

            AnatomyModelRecord record = new AnatomyModelRecord
            {
                ModelId = definition.ModelId,
                DisplayName = definition.DisplayName,
                Species = definition.Species,
                Bone = definition.Bone,
                Description = definition.Description,
                PrefabKey = definition.ModelPrefab != null
                    ? definition.ModelPrefab.name
                    : ""
            };

            AnatomyDatabase.InsertOrUpdate(record);
            registered++;

            Debug.Log(
                $"Modelo sincronizado: {record.DisplayName} | Prefab: {record.PrefabKey}"
            );
        }

        Debug.Log($"Catálogo sincronizado: {registered} modelos.");
    }
}