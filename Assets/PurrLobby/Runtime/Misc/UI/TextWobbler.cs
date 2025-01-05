using UnityEngine;
using TMPro;

public class TextWobbler : MonoBehaviour
{
    [SerializeField] private TMP_Text textMesh;
    [SerializeField] private float moveAmount = 0.5f;
    [SerializeField] private float waveDistance = 2f;
    [SerializeField] private float speed = 1f;
    
    private void Start()
    {
        if (textMesh == null)
            textMesh = GetComponent<TMP_Text>();
    }

    private void Update()
    {
        textMesh.ForceMeshUpdate();
        var textInfo = textMesh.textInfo;

        for (int i = 0; i < textInfo.characterCount; i++)
        {
            var charInfo = textInfo.characterInfo[i];
            if (!charInfo.isVisible) 
                continue;

            var vIndex = charInfo.vertexIndex;
            var meshInfo = textInfo.meshInfo[charInfo.materialReferenceIndex];
            
            for (int j = 0; j < 4; j++)
            {
                var orig = meshInfo.vertices[vIndex + j];
                meshInfo.vertices[vIndex + j] = orig + new Vector3(0, 
                    Mathf.Sin(Time.time * speed + i * waveDistance) * moveAmount, 
                    0);
            }
        }

        for (int i = 0; i < textInfo.meshInfo.Length; i++)
        {
            var meshInfo = textInfo.meshInfo[i];
            if (meshInfo.mesh)
            {
                meshInfo.mesh.vertices = meshInfo.vertices;
                textMesh.UpdateGeometry(meshInfo.mesh, i);
            }
        }
    }
}