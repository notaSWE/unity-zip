using UnityEngine;
using System;
using System.Collections.Generic;
using TMPro;

[Serializable]
public class GridData
{
    public int width;
    public int height;
    public int[][] cells;
    public bool[][] blockRight;
    public bool[][] blockUp;
}

public class GridManager : MonoBehaviour
{
    [Header("Grid")]
    [Min(1)] public int width = 7;
    [Min(1)] public int height = 7;
    [Min(0.01f)] public float cellSize = 1f;
    public Vector2 originWorld = Vector2.zero;

    [Header("Visuals")]
    public CellView cellPrefab;
    public Transform cellParent;
    public GameObject cellBlockRightPrefab;
    public GameObject cellBlockUpPrefab;

    [Header("Optional JSON")]
    public TextAsset gridJsonFile;

    private CellView[,] _cells;

    void Start()
    {
        // Automatically build the grid when entering play mode
        BuildGrid();
    }

    public void BuildGrid()
    {
        ClearGrid();

        // If JSON file is provided, load grid data from it
        if (gridJsonFile != null && !string.IsNullOrEmpty(gridJsonFile.text))
        {
            BuildGridFromJson(gridJsonFile.text);
        }
        else
        {
            BuildGridDefault();
        }
    }

    private void BuildGridDefault()
    {
        _cells = new CellView[width, height];

        if (cellParent == null) cellParent = transform;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Vector2 pos = GridToWorld(x, y);
                var cell = Instantiate(cellPrefab, pos, Quaternion.identity, cellParent);
                cell.name = $"Cell_{x}_{y}";
                cell.Init(x, y);

                _cells[x, y] = cell;
            }
        }
    }

    private void BuildGridFromJson(string jsonText)
    {
        try
        {
            // Parse JSON manually since Unity's JsonUtility doesn't handle nested arrays well
            GridData gridData = ParseGridJson(jsonText);

            // Use JSON dimensions if provided, otherwise use inspector values
            int gridWidth = gridData.width > 0 ? gridData.width : width;
            int gridHeight = gridData.height > 0 ? gridData.height : height;

            _cells = new CellView[gridWidth, gridHeight];

            if (cellParent == null) cellParent = transform;

            for (int y = 0; y < gridHeight; y++)
            {
                for (int x = 0; x < gridWidth; x++)
                {
                    // Flip vertically: JSON row 0 goes to top (gridHeight-1), row gridHeight-1 goes to bottom (0)
                    int worldY = gridHeight - 1 - y;
                    Vector2 pos = GridToWorld(x, worldY);
                    var cell = Instantiate(cellPrefab, pos, Quaternion.identity, cellParent);
                    cell.name = $"Cell_{x}_{y}";
                    cell.Init(x, y);

                    // Set cell number from JSON if available (use original y to read correct JSON row)
                    int cellValue = 0;
                    if (gridData.cells != null && 
                        y < gridData.cells.Length && 
                        x < gridData.cells[y].Length)
                    {
                        cellValue = gridData.cells[y][x];
                    }
                    
                    // Set the Number property on the CellView (important for game logic)
                    cell.SetNumber(cellValue);
                    
                    // Set the Value TextMeshPro child - show number if not zero, clear if zero
                    // Try direct child first, then recursive search
                    Transform valueTransform = cell.transform.Find("Value");
                    if (valueTransform == null)
                    {
                        // Try recursive search
                        valueTransform = FindChildRecursive(cell.transform, "Value");
                    }
                    
                    if (valueTransform != null)
                    {
                        TMP_Text valueText = valueTransform.GetComponent<TMP_Text>();
                        if (valueText != null)
                        {
                            if (cellValue != 0)
                            {
                                Debug.Log($"Cell [{x}, {y}] value from JSON: {cellValue}");
                                valueText.text = cellValue.ToString();
                            }
                            else
                            {
                                valueText.text = ""; // Clear the "T" for zero values
                            }
                        }
                        else
                        {
                            Debug.LogWarning($"Cell [{x}, {y}]: Value transform found but TMP_Text component is null");
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"Cell [{x}, {y}]: Value child transform not found. Child count: {cell.transform.childCount}");
                        // List all children for debugging
                        for (int i = 0; i < cell.transform.childCount; i++)
                        {
                            Debug.LogWarning($"  Child {i}: {cell.transform.GetChild(i).name}");
                        }
                    }

                    _cells[x, worldY] = cell;
                }
            }

            // Add block prefabs after grid is built
            AddBlockPrefabs(gridData, gridWidth, gridHeight);
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to build grid from JSON: {e.Message}");
            Debug.LogError($"Falling back to default grid build.");
            BuildGridDefault();
        }
    }

    private void AddBlockPrefabs(GridData gridData, int gridWidth, int gridHeight)
    {
        if (cellBlockRightPrefab == null && cellBlockUpPrefab == null)
            return;

        for (int y = 0; y < gridHeight; y++)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                // Flip vertically: JSON row 0 maps to worldY gridHeight-1
                int worldY = gridHeight - 1 - y;
                CellView cell = _cells[x, worldY];

                if (cell == null) continue;

                // Check blockRight
                if (cellBlockRightPrefab != null && 
                    gridData.blockRight != null && 
                    y < gridData.blockRight.Length && 
                    x < gridData.blockRight[y].Length &&
                    gridData.blockRight[y][x])
                {
                    GameObject blockRight = Instantiate(cellBlockRightPrefab, cell.transform);
                    // Position on the right side of the cell (offset by half cell size + 45px to account for block width)
                    // 45px = 0.45 units (assuming 100 pixels per unit)
                    blockRight.transform.localPosition = new Vector3(cellSize * 0.5f + 0.45f, 0f, -0.1f);
                    // Bring to front visually (closer to camera)
                    SetSpriteToFront(blockRight);
                }

                // Check blockUp
                if (cellBlockUpPrefab != null && 
                    gridData.blockUp != null && 
                    y < gridData.blockUp.Length && 
                    x < gridData.blockUp[y].Length &&
                    gridData.blockUp[y][x])
                {
                    GameObject blockUp = Instantiate(cellBlockUpPrefab, cell.transform);
                    // Position on the top of the cell (offset by half cell size + 45px to account for block width)
                    // 45px = 0.45 units (assuming 100 pixels per unit)
                    blockUp.transform.localPosition = new Vector3(0f, cellSize * 0.5f + 0.45f, -0.1f);
                    // Bring to front visually (closer to camera)
                    SetSpriteToFront(blockUp);
                }
            }
        }
    }

    private GridData ParseGridJson(string jsonText)
    {
        GridData data = new GridData();

        // Extract width
        int widthIndex = jsonText.IndexOf("\"width\"");
        if (widthIndex >= 0)
        {
            widthIndex = jsonText.IndexOf(':', widthIndex) + 1;
            // Skip whitespace
            while (widthIndex < jsonText.Length && char.IsWhiteSpace(jsonText[widthIndex]))
                widthIndex++;
            
            int endIndex = widthIndex;
            while (endIndex < jsonText.Length && 
                   jsonText[endIndex] != ',' && 
                   jsonText[endIndex] != '}' && 
                   jsonText[endIndex] != '\n' && 
                   jsonText[endIndex] != '\r')
                endIndex++;
            
            if (endIndex > widthIndex)
            {
                string widthStr = jsonText.Substring(widthIndex, endIndex - widthIndex).Trim();
                int.TryParse(widthStr, out data.width);
            }
        }

        // Extract height
        int heightIndex = jsonText.IndexOf("\"height\"");
        if (heightIndex >= 0)
        {
            heightIndex = jsonText.IndexOf(':', heightIndex) + 1;
            // Skip whitespace
            while (heightIndex < jsonText.Length && char.IsWhiteSpace(jsonText[heightIndex]))
                heightIndex++;
            
            int endIndex = heightIndex;
            while (endIndex < jsonText.Length && 
                   jsonText[endIndex] != ',' && 
                   jsonText[endIndex] != '}' && 
                   jsonText[endIndex] != '\n' && 
                   jsonText[endIndex] != '\r')
                endIndex++;
            
            if (endIndex > heightIndex)
            {
                string heightStr = jsonText.Substring(heightIndex, endIndex - heightIndex).Trim();
                int.TryParse(heightStr, out data.height);
            }
        }

        // Extract cells array
        int cellsStart = jsonText.IndexOf("\"cells\"");
        if (cellsStart >= 0)
        {
            cellsStart = jsonText.IndexOf('[', cellsStart);
            if (cellsStart >= 0)
            {
                int cellsEnd = FindMatchingBracket(jsonText, cellsStart);
                if (cellsEnd > cellsStart)
                {
                    string cellsJson = jsonText.Substring(cellsStart, cellsEnd - cellsStart + 1);
                    data.cells = ParseNestedIntArray(cellsJson);
                }
            }
        }

        // Extract blockRight array
        int blockRightStart = jsonText.IndexOf("\"blockRight\"");
        if (blockRightStart >= 0)
        {
            blockRightStart = jsonText.IndexOf('[', blockRightStart);
            if (blockRightStart >= 0)
            {
                int blockRightEnd = FindMatchingBracket(jsonText, blockRightStart);
                if (blockRightEnd > blockRightStart)
                {
                    string blockRightJson = jsonText.Substring(blockRightStart, blockRightEnd - blockRightStart + 1);
                    data.blockRight = ParseNestedBoolArray(blockRightJson);
                }
            }
        }

        // Extract blockUp array
        int blockUpStart = jsonText.IndexOf("\"blockUp\"");
        if (blockUpStart >= 0)
        {
            blockUpStart = jsonText.IndexOf('[', blockUpStart);
            if (blockUpStart >= 0)
            {
                int blockUpEnd = FindMatchingBracket(jsonText, blockUpStart);
                if (blockUpEnd > blockUpStart)
                {
                    string blockUpJson = jsonText.Substring(blockUpStart, blockUpEnd - blockUpStart + 1);
                    data.blockUp = ParseNestedBoolArray(blockUpJson);
                }
            }
        }

        return data;
    }

    private int[][] ParseNestedIntArray(string arrayJson)
    {
        System.Collections.Generic.List<int[]> rows = new System.Collections.Generic.List<int[]>();

        int depth = 0;
        int start = -1;

        for (int i = 0; i < arrayJson.Length; i++)
        {
            char c = arrayJson[i];
            
            if (c == '[')
            {
                if (depth == 1) start = i; // Start of a row
                depth++;
            }
            else if (c == ']')
            {
                depth--;
                if (depth == 1 && start >= 0) // End of a row
                {
                    string rowJson = arrayJson.Substring(start, i - start + 1);
                    rows.Add(ParseIntArray(rowJson));
                    start = -1;
                }
            }
        }

        return rows.ToArray();
    }

    private int[] ParseIntArray(string arrayJson)
    {
        System.Collections.Generic.List<int> values = new System.Collections.Generic.List<int>();
        
        int start = 0;
        bool inNumber = false;

        for (int i = 0; i < arrayJson.Length; i++)
        {
            char c = arrayJson[i];
            
            if (char.IsDigit(c) || c == '-')
            {
                if (!inNumber)
                {
                    start = i;
                    inNumber = true;
                }
            }
            else if (inNumber)
            {
                string numStr = arrayJson.Substring(start, i - start);
                if (int.TryParse(numStr, out int value))
                {
                    values.Add(value);
                }
                inNumber = false;
            }
        }
        
        // Handle number at end of string
        if (inNumber)
        {
            string numStr = arrayJson.Substring(start);
            if (int.TryParse(numStr, out int value))
            {
                values.Add(value);
            }
        }

        return values.ToArray();
    }

    private bool[][] ParseNestedBoolArray(string arrayJson)
    {
        System.Collections.Generic.List<bool[]> rows = new System.Collections.Generic.List<bool[]>();

        int depth = 0;
        int start = -1;

        for (int i = 0; i < arrayJson.Length; i++)
        {
            char c = arrayJson[i];
            
            if (c == '[')
            {
                if (depth == 1) start = i; // Start of a row
                depth++;
            }
            else if (c == ']')
            {
                depth--;
                if (depth == 1 && start >= 0) // End of a row
                {
                    string rowJson = arrayJson.Substring(start, i - start + 1);
                    rows.Add(ParseBoolArray(rowJson));
                    start = -1;
                }
            }
        }

        return rows.ToArray();
    }

    private bool[] ParseBoolArray(string arrayJson)
    {
        System.Collections.Generic.List<bool> values = new System.Collections.Generic.List<bool>();
        
        // Look for "true" and "false" strings
        string lowerJson = arrayJson.ToLower();
        int index = 0;
        
        while (index < lowerJson.Length)
        {
            int trueIndex = lowerJson.IndexOf("true", index);
            int falseIndex = lowerJson.IndexOf("false", index);
            
            if (trueIndex >= 0 && (falseIndex < 0 || trueIndex < falseIndex))
            {
                values.Add(true);
                index = trueIndex + 4; // "true" is 4 characters
            }
            else if (falseIndex >= 0)
            {
                values.Add(false);
                index = falseIndex + 5; // "false" is 5 characters
            }
            else
            {
                break;
            }
        }

        return values.ToArray();
    }

    private int FindMatchingBracket(string text, int startIndex)
    {
        int depth = 0;
        for (int i = startIndex; i < text.Length; i++)
        {
            if (text[i] == '[') depth++;
            else if (text[i] == ']')
            {
                depth--;
                if (depth == 0) return i;
            }
        }
        return -1;
    }

    private Transform FindChildRecursive(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name)
                return child;
            
            Transform found = FindChildRecursive(child, name);
            if (found != null)
                return found;
        }
        return null;
    }

    private void SetSpriteToFront(GameObject obj)
    {
        // Set sorting order if using SpriteRenderer (higher = rendered on top)
        SpriteRenderer spriteRenderer = obj.GetComponent<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            spriteRenderer.sortingOrder = 10;
        }

        // Check children for SpriteRenderers too
        SpriteRenderer[] childRenderers = obj.GetComponentsInChildren<SpriteRenderer>();
        foreach (SpriteRenderer renderer in childRenderers)
        {
            renderer.sortingOrder = 10;
        }
    }

    public Vector2 GridToWorld(int x, int y)
        => originWorld + new Vector2(x * cellSize, y * cellSize);

    public Vector2Int WorldToGrid(Vector2 world)
    {
        Vector2 local = world - originWorld;
        int x = Mathf.RoundToInt(local.x / cellSize);
        int y = Mathf.RoundToInt(local.y / cellSize);
        return new Vector2Int(x, y);
    }

    public bool InBounds(int x, int y)
        => x >= 0 && y >= 0 && x < width && y < height;

    public CellView GetCellAtWorldPosition(Vector2 worldPos)
    {
        if (_cells == null) return null;
        
        Vector2Int gridPos = WorldToGrid(worldPos);
        
        // Check actual array bounds instead of using InBounds (which uses inspector values)
        if (gridPos.x >= 0 && gridPos.y >= 0 && 
            gridPos.x < _cells.GetLength(0) && gridPos.y < _cells.GetLength(1))
        {
            Debug.Log($"GetCellAtWorldPosition: World pos {worldPos} -> Grid pos [{gridPos.x}, {gridPos.y}], Array size: [{_cells.GetLength(0)}, {_cells.GetLength(1)}]");
            return _cells[gridPos.x, gridPos.y];
        }
        
        Debug.Log($"GetCellAtWorldPosition: World pos {worldPos} -> Grid pos [{gridPos.x}, {gridPos.y}] is out of bounds. Array size: [{_cells.GetLength(0)}, {_cells.GetLength(1)}]");
        return null;
    }

    // Get all cells in the grid
    public List<CellView> GetAllCells()
    {
        List<CellView> allCells = new List<CellView>();
        if (_cells == null) return allCells;
        
        for (int y = 0; y < _cells.GetLength(1); y++)
        {
            for (int x = 0; x < _cells.GetLength(0); x++)
            {
                if (_cells[x, y] != null)
                {
                    allCells.Add(_cells[x, y]);
                }
            }
        }
        
        return allCells;
    }

    // Get the actual grid dimensions (may differ from inspector values if loaded from JSON)
    public int GetGridWidth() => _cells != null ? _cells.GetLength(0) : width;
    public int GetGridHeight() => _cells != null ? _cells.GetLength(1) : height;

    private void ClearGrid()
    {
        if (cellParent == null) return;

        for (int i = cellParent.childCount - 1; i >= 0; i--)
            DestroyImmediate(cellParent.GetChild(i).gameObject);
    }

    // Public method for UI buttons to clear the grid
    public void ClearGridUI()
    {
        ClearGrid();
    }

#if UNITY_EDITOR
    // Convenience buttons in Inspector
    [ContextMenu("Build Grid")]
    private void BuildGridContextMenu() => BuildGrid();
    
    [ContextMenu("Clear Grid")]
    private void ClearGridContextMenu() => ClearGrid();
#endif
}
