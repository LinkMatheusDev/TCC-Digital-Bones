
using System;
using UnityEngine;

[DefaultExecutionOrder(100)]
public class AnatomyModelLoader : MonoBehaviour
{
    [SerializeField] private AnatomyModelDefinition[] catalog;
    [SerializeField] private Transform modelContainer;
    [SerializeField] private AnatomyViewer viewer;

    private GameObject currentModel;

    public AnatomyModelDefinition[] Catalog => catalog;

    private void Start()
    {
        AnatomyDatabase.Initialize();

        if (viewer == null)
        {
            viewer = FindObjectOfType<AnatomyViewer>();
        }
    }

    public void LoadModel(string modelId)
    {
        TryLoadModel(modelId);
    }

    public bool TryLoadModel(string modelId)
    {
        AnatomyModelRecord record =
            AnatomyDatabase.GetModelById(modelId);

        if (record == null)
        {
            Debug.LogError(
                "Modelo não encontrado no banco: " + modelId
            );
            return false;
        }

        AnatomyModelDefinition definition = Array.Find(
            catalog ?? Array.Empty<AnatomyModelDefinition>(),
            item => item != null &&
                    item.ModelId == record.ModelId
        );

        if (definition == null || definition.ModelPrefab == null)
        {
            Debug.LogError(
                "Prefab não encontrado para: " + record.DisplayName
            );
            return false;
        }

        if (modelContainer == null)
        {
            Debug.LogError(
                "Model Container não foi associado!"
            );
            return false;
        }

        if (viewer == null)
        {
            Debug.LogError(
                "AnatomyViewer não foi encontrado na cena!"
            );
            return false;
        }

        if (currentModel != null)
        {
            Destroy(currentModel);
        }

        currentModel = Instantiate(
            definition.ModelPrefab,
            modelContainer,
            false
        );

        currentModel.name = record.DisplayName;

        // Entrega o modelo recém-carregado ao visualizador.
        viewer.SetModelRoot(currentModel.transform);

        Debug.Log(
            "Modelo carregado e conectado ao visualizador: " +
            record.DisplayName
        );

        return true;
    }
}