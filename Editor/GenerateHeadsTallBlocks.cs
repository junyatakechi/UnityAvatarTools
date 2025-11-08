using UnityEngine;
using UnityEditor;

namespace JayT.UnityAvatarTools.Editor
{
    public class GenerateHeadsTallBlocks : EditorWindow
    {
        private float heightCm = 170f; // 身長(cm)
        private float headsTall = 7f; // 等身
        private float headSizeCm = 0f; // 頭のサイズ(cm)
        [SerializeField] private Material cubeMaterial; // Cubeに適用するマテリアル

        [MenuItem("Tools/JayT/UnityAvatarTools/Generate Heads Tall Blocks")]
        public static void ShowWindow()
        {
            GetWindow<GenerateHeadsTallBlocks>("Generate Heads Tall Blocks");
        }

        private void OnGUI()
        {
            GUILayout.Label("Head Size Settings", EditorStyles.boldLabel);
            heightCm = EditorGUILayout.FloatField("Height (cm)", heightCm);
            headsTall = EditorGUILayout.FloatField("Heads Tall", headsTall);

            // 計算結果を表示
            headSizeCm = heightCm / headsTall;
            EditorGUILayout.LabelField("Head Size(cm)", $"{headSizeCm}");

            GUILayout.Space(10);
            cubeMaterial = (Material)EditorGUILayout.ObjectField("Cube Material", cubeMaterial, typeof(Material), false);

            if (GUILayout.Button("Generate"))
            {
                GenerateBlocks();
            }
        }

private void GenerateBlocks()
{
    float headSize = headSizeCm / 100f;
    int fullBlocks = Mathf.FloorToInt(headsTall); // 小数部分を除いた整数分
    float fractionalPart = headsTall - fullBlocks; // 余り（例：6.5 -> 0.5）

    GameObject root = new GameObject($"HeadsTallBlocks_{heightCm}cm_{headsTall}Heads");
    Undo.RegisterCreatedObjectUndo(root, "Generate Heads Tall Blocks");

    // --- 通常の等身キューブ（縦） ---
    GameObject group = new GameObject($"Group_Head{headSizeCm}cm_Vertical");
    group.transform.SetParent(root.transform);
    Undo.RegisterCreatedObjectUndo(group, "Generate Heads Tall Blocks");

    float currentY = 0f;

    // ① 余り分のキューブ（部分ブロック）→ 一番下に配置
    if (fractionalPart > 0f)
    {
        float partialHeight = headSize * fractionalPart;
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = $"Block_Partial_{fractionalPart:F2}";
        cube.transform.SetParent(group.transform);
        cube.transform.localScale = new Vector3(headSize, partialHeight, headSize);
        cube.transform.localPosition = new Vector3(0, partialHeight * 0.5f, 0);
        ApplyMaterial(cube);
        Undo.RegisterCreatedObjectUndo(cube, "Generate Heads Tall Blocks");

        currentY += partialHeight; // 現在の高さを更新
    }

    // ② 整数分のキューブをその上に積み上げる
    for (int i = 0; i < fullBlocks; i++)
    {
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = $"Block_{i + 1}";
        cube.transform.SetParent(group.transform);
        cube.transform.localScale = new Vector3(headSize, headSize, headSize);
        cube.transform.localPosition = new Vector3(0, currentY + headSize * 0.5f, 0);
        ApplyMaterial(cube);
        Undo.RegisterCreatedObjectUndo(cube, "Generate Heads Tall Blocks");

        currentY += headSize; // 高さ更新
    }

    // --- 身長1/4キューブの4段 ---
    float quarterHeightSize = (heightCm / 4f) / 100f;
    CreateVerticalCubeGroup(root, $"Group_Quarter{heightCm / 4f}cm", headSize, quarterHeightSize, 4, headSize);

    // --- 横に並ぶ等身キューブ ---
    float topY = (heightCm / 100f) - (headSize / 2f);
    CreateHorizontalCubeGroup(root, $"Group_Head{headSizeCm}cm_Horizontal", headSize, fullBlocks + (fractionalPart > 0 ? 1 : 0), topY);

    // root.transform.position = new Vector3(headSize * 2f, 0, 0);
    Selection.activeGameObject = root;
}

        private void CreateVerticalCubeGroup(GameObject parent, string groupName, float cubeXZSize, float cubeYSize, int blockCount, float xOffset)
        {
            GameObject group = new GameObject(groupName);
            group.transform.SetParent(parent.transform);
            Undo.RegisterCreatedObjectUndo(group, "Generate Heads Tall Blocks");

            for (int i = 0; i < blockCount; i++)
            {
                GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = $"Block_{i + 1}";
                cube.transform.SetParent(group.transform);
                cube.transform.localScale = new Vector3(cubeXZSize, cubeYSize, cubeXZSize);
                cube.transform.localPosition = new Vector3(xOffset, cubeYSize * i + cubeYSize * 0.5f, 0);

                ApplyMaterial(cube);
                
                Undo.RegisterCreatedObjectUndo(cube, "Generate Heads Tall Blocks");
            }
        }

        private void CreateHorizontalCubeGroup(GameObject parent, string groupName, float cubeSize, int blockCount, float yOffset)
        {
            GameObject group = new GameObject(groupName);
            group.transform.SetParent(parent.transform);
            Undo.RegisterCreatedObjectUndo(group, "Generate Heads Tall Blocks");

            // 中心から左右に配置するため、開始位置を調整
            float startX = -(blockCount * cubeSize) / 2f + cubeSize * 0.5f;

            for (int i = 0; i < blockCount; i++)
            {
                GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = $"Block_{i + 1}";
                cube.transform.SetParent(group.transform);
                cube.transform.localScale = new Vector3(cubeSize, cubeSize, cubeSize);
                cube.transform.localPosition = new Vector3(startX + cubeSize * i, yOffset, 0);

                ApplyMaterial(cube);
                
                Undo.RegisterCreatedObjectUndo(cube, "Generate Heads Tall Blocks");
            }
        }

        private void ApplyMaterial(GameObject cube)
        {
            if (cubeMaterial != null)
            {
                Renderer renderer = cube.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = cubeMaterial;
                }
            }
        }
    }
}