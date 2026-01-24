using System.Collections.Generic;
using UnityEngine;

public class ZipInputController : MonoBehaviour
{
    public GridManager grid;
    public LayerMask cellMask;
    public LayerMask barrierMask;
    public LineRenderer snakeLine;
    public float snakeZ = -10.1f; // or +0.1f depending on your camera

    private readonly List<CellView> path = new();
    private readonly Stack<int> visitedNumbers = new();

    private bool isDrawing;
    private int nextNumberExpected = 2;

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
            TryStart();

        if (Input.GetMouseButton(0) && isDrawing)
            TryExtendOrRewind();

        if (Input.GetMouseButtonUp(0))
            isDrawing = false;
    }

    void TryStart()
    {
        var cell = GetCellUnderMouse();
        
        if (cell == null)
            return;
        
        if (cell.Number != 1)
            return;

        ClearPath();
        visitedNumbers.Clear();

        AddCell(cell);               // will mark visited; since it's 1, we should treat it as already satisfied
        nextNumberExpected = 2;      // next target after 1
        isDrawing = true;
    }

    void TryExtendOrRewind()
    {
        var cell = GetCellUnderMouse();
        if (cell == null) return;
        if (path.Count == 0) return;

        var current = path[^1];
        if (cell == current) return;

        // rewind if hovering previous cell (single-step rewind)
        if (path.Count >= 2 && cell == path[^2])
        {
            RemoveLastCell();
            return;
        }

        // (Optional upgrade) multi-step rewind: if hovering any earlier cell in the path, rewind to it.
        // Uncomment this block if you want that behavior.
        /*
        int idx = path.LastIndexOf(cell);
        if (idx >= 0)
        {
            while (path.Count - 1 > idx)
                RemoveLastCell();
            return;
        }
        */

        if (!IsAdjacent(current, cell)) return;
        if (cell.IsVisited) return;
        if (IsBlocked(current, cell)) return;

        // Number rule removed - can move to any cell regardless of number

        AddCell(cell);
        
        // Check if puzzle is solved after adding cell
        if (IsPuzzleSolved())
        {
            Debug.Log("PUZZLE SOLVED!");
            // TODO: Add win condition handling (show message, disable input, etc.)
        }
    }

    CellView GetCellUnderMouse()
    {
        Vector3 mousePos = Input.mousePosition;
        
        // For 2D cameras, use the camera's Z distance
        if (Camera.main.orthographic)
        {
            mousePos.z = Camera.main.transform.position.z;
        }
        else
        {
            mousePos.z = Mathf.Abs(Camera.main.transform.position.z);
        }
        
        var world = (Vector2)Camera.main.ScreenToWorldPoint(mousePos);
        
        // Get all colliders at this point (most reliable method)
        var allColliders = Physics2D.OverlapPointAll(world);
        
        // Filter for colliders that have a CellView component
        // If cellMask is set (non-zero), also check that the layer matches
        foreach (var col in allColliders)
        {
            // If cellMask is set, check layer first; otherwise check all colliders
            bool layerMatches = cellMask.value == 0 || (cellMask.value & (1 << col.gameObject.layer)) != 0;
            
            if (layerMatches)
            {
                var cell = col.GetComponent<CellView>();
                if (cell != null)
                {
                    return cell;
                }
            }
        }
        
        // Fallback: Use GridManager to find cell by world position
        if (grid != null)
        {
            var cell = grid.GetCellAtWorldPosition(world);
            if (cell != null)
            {
                return cell;
            }
        }
        
        return null;
    }

    bool IsAdjacent(CellView a, CellView b)
        => Mathf.Abs(a.X - b.X) + Mathf.Abs(a.Y - b.Y) == 1;

    bool IsBlocked(CellView from, CellView to)
    {
        Vector2 a = from.transform.position;
        Vector2 b = to.transform.position;
        Vector2 dir = b - a;
        float dist = dir.magnitude;

        if (dist < 0.001f) return false; // Same cell, not blocked

        // Determine movement direction: horizontal (left/right) or vertical (up/down)
        bool isHorizontalMovement = Mathf.Abs(dir.x) > Mathf.Abs(dir.y);
        
        Debug.Log($"IsBlocked: Checking from [{from.X}, {from.Y}] to [{to.X}, {to.Y}], horizontal={isHorizontalMovement}, barrierMask={barrierMask.value}");

        // Use OverlapArea to check the entire area between cells - most reliable method
        Vector2 perpendicular = new Vector2(-dir.y, dir.x).normalized; // Perpendicular to movement direction
        Vector2 midPoint = (a + b) * 0.5f;
        float areaSize = 0.5f; // Wider area to catch edge-positioned barriers
        
        // Create a wider overlap area that covers the path between cells
        Vector2 corner1 = midPoint - perpendicular * areaSize - dir.normalized * (dist * 0.4f);
        Vector2 corner2 = midPoint + perpendicular * areaSize + dir.normalized * (dist * 0.4f);
        
        Collider2D[] overlaps = Physics2D.OverlapAreaAll(corner1, corner2, barrierMask);
        
        Debug.Log($"IsBlocked: Found {overlaps.Length} colliders in overlap area between [{from.X}, {from.Y}] and [{to.X}, {to.Y}]");
        Debug.Log($"IsBlocked: Overlap area corners: {corner1} to {corner2}");

        // Also directly check children of source and destination cells for barriers
        // This ensures we catch barriers even if the overlap area misses them
        Debug.Log($"IsBlocked: Checking source cell [{from.X}, {from.Y}] children for barriers...");
        if (CheckCellChildrenForBarriers(from, to, isHorizontalMovement, dir, true))
        {
            Debug.Log($"IsBlocked: Source cell check returned TRUE - movement blocked!");
            return true;
        }
        Debug.Log($"IsBlocked: Checking destination cell [{to.X}, {to.Y}] children for barriers...");
        if (CheckCellChildrenForBarriers(to, from, isHorizontalMovement, dir, false))
        {
            Debug.Log($"IsBlocked: Destination cell check returned TRUE - movement blocked!");
            return true;
        }

        foreach (var col in overlaps)
        {
            string barrierName = col.gameObject.name;
            int barrierLayer = col.gameObject.layer;
            bool isCell = col.GetComponent<CellView>() != null;
            
            Debug.Log($"IsBlocked: Collider '{barrierName}' on layer {barrierLayer}, isCell={isCell}, enabled={col.enabled}");
            
            // Ignore the source and destination cell colliders
            if (col.GetComponent<CellView>() == from || col.GetComponent<CellView>() == to)
            {
                Debug.Log($"IsBlocked: Skipping cell collider '{barrierName}'");
                continue;
            }
            
            bool isCellBlockUp = barrierName.Contains("CellBlockUp");
            bool isCellBlockRight = barrierName.Contains("CellBlockRight");
            
            // Find which cell this barrier belongs to (it's a child of a cell)
            CellView barrierCell = col.GetComponentInParent<CellView>();
            if (barrierCell == null)
            {
                Debug.Log($"IsBlocked: Barrier '{barrierName}' has no parent CellView");
                continue;
            }
            
            Debug.Log($"IsBlocked: Found barrier '{barrierName}' on layer {barrierLayer}, belongs to cell [{barrierCell.X}, {barrierCell.Y}], isCellBlockUp={isCellBlockUp}, isCellBlockRight={isCellBlockRight}");
            
            // CellBlockUp blocks vertical movement
            // CellBlockUp is positioned at the TOP of a cell, so it blocks:
            // - Movement TO that cell from above (moving down into it)
            // - Movement FROM that cell going up (moving up out of it)
            if (isCellBlockUp && !isHorizontalMovement)
            {
                bool isMovingDown = dir.y < 0;
                bool isMovingUp = dir.y > 0;
                
                Debug.Log($"IsBlocked: OverlapArea CellBlockUp check - isMovingDown={isMovingDown}, isMovingUp={isMovingUp}, barrierCell=[{barrierCell.X}, {barrierCell.Y}], from=[{from.X}, {from.Y}], to=[{to.X}, {to.Y}]");
                
                // CellBlockUp on destination blocks downward movement (moving down into destination)
                // CellBlockUp on source blocks upward movement (moving up out of source)
                if ((isMovingDown && barrierCell == to) || (isMovingUp && barrierCell == from))
                {
                    Debug.Log($"IsBlocked: OverlapArea - CellBlockUp on cell [{barrierCell.X}, {barrierCell.Y}] blocking vertical movement from [{from.X}, {from.Y}] to [{to.X}, {to.Y}]");
                    return true;
                }
                else
                {
                    Debug.Log($"IsBlocked: OverlapArea CellBlockUp found but not blocking - barrier on [{barrierCell.X}, {barrierCell.Y}], movingDown={isMovingDown}, movingUp={isMovingUp}, barrierCell==to={barrierCell == to}, barrierCell==from={barrierCell == from}");
                }
            }
            
            // CellBlockRight blocks horizontal movement
            // CellBlockRight on a cell is at the RIGHT of that cell, so it blocks movement FROM that cell going right
            // OR movement TO that cell from the left (if the barrier is on the destination)
            if (isCellBlockRight && isHorizontalMovement)
            {
                bool isMovingRight = dir.x > 0;
                bool isMovingLeft = dir.x < 0;
                
                // CellBlockRight on the source cell blocks movement FROM it going right
                if (isMovingRight && barrierCell == from)
                {
                    Debug.Log($"IsBlocked: CellBlockRight on source cell [{barrierCell.X}, {barrierCell.Y}] blocking rightward movement from [{from.X}, {from.Y}] to [{to.X}, {to.Y}]");
                    return true;
                }
                // CellBlockRight on the destination cell blocks movement TO it from the left (moving right)
                else if (isMovingRight && barrierCell == to)
                {
                    Debug.Log($"IsBlocked: CellBlockRight on destination cell [{barrierCell.X}, {barrierCell.Y}] blocking rightward movement from [{from.X}, {from.Y}] to [{to.X}, {to.Y}]");
                    return true;
                }
                // Moving left - CellBlockRight on source cell blocks leftward movement
                else if (isMovingLeft && barrierCell == from)
                {
                    Debug.Log($"IsBlocked: CellBlockRight on source cell [{barrierCell.X}, {barrierCell.Y}] blocking leftward movement from [{from.X}, {from.Y}] to [{to.X}, {to.Y}]");
                    return true;
                }
                else
                {
                    Debug.Log($"IsBlocked: CellBlockRight found but on wrong cell - barrier on [{barrierCell.X}, {barrierCell.Y}], from [{from.X}, {from.Y}] to [{to.X}, {to.Y}], movingRight={isMovingRight}, movingLeft={isMovingLeft}");
                }
            }
        }

        // Also try raycast as backup
        Vector2 start = a + dir.normalized * 0.1f;
        float safeDist = dist - 0.2f;
        
        if (safeDist > 0)
        {
            RaycastHit2D hit = Physics2D.Raycast(start, dir.normalized, safeDist, barrierMask);
            if (hit.collider != null)
            {
                string barrierName = hit.collider.gameObject.name;
                bool isCellBlockUp = barrierName.Contains("CellBlockUp");
                bool isCellBlockRight = barrierName.Contains("CellBlockRight");
                
                Debug.Log($"IsBlocked: Raycast hit '{barrierName}', isCellBlockUp={isCellBlockUp}, isCellBlockRight={isCellBlockRight}, horizontal={isHorizontalMovement}");
                
                if (isCellBlockUp && !isHorizontalMovement)
                {
                    return true;
                }
                
                if (isCellBlockRight && isHorizontalMovement)
                {
                    return true;
                }
            }
        }

        Debug.Log($"IsBlocked: No blocking barrier found between [{from.X}, {from.Y}] and [{to.X}, {to.Y}]");
        return false;
    }

    bool CheckCellChildrenForBarriers(CellView cell, CellView otherCell, bool isHorizontalMovement, Vector2 dir, bool isSourceCell)
    {
        if (cell == null)
        {
            Debug.Log($"IsBlocked: CheckCellChildrenForBarriers - cell is null!");
            return false;
        }
        
        Debug.Log($"IsBlocked: CheckCellChildrenForBarriers - Checking cell [{cell.X}, {cell.Y}], childCount={cell.transform.childCount}");
        
        // Check all children of this cell for barrier components
        for (int i = 0; i < cell.transform.childCount; i++)
        {
            Transform child = cell.transform.GetChild(i);
            Debug.Log($"IsBlocked: Checking child {i}: '{child.name}'");
            
            // Try GetComponent first, then GetComponentInChildren in case collider is nested
            Collider2D col = child.GetComponent<Collider2D>();
            if (col == null)
            {
                col = child.GetComponentInChildren<Collider2D>();
                if (col != null)
                {
                    Debug.Log($"IsBlocked: Found Collider2D in children of '{child.name}'");
                }
            }
            
            if (col == null)
            {
                Debug.Log($"IsBlocked: Child '{child.name}' has no Collider2D (checked self and children)");
                continue;
            }
            
            if (!col.enabled)
            {
                Debug.Log($"IsBlocked: Child '{child.name}' collider is disabled");
                continue;
            }
            
            // Check if this collider is on the barrier layer
            int layer = col.gameObject.layer;
            int layerBit = 1 << layer;
            bool layerMatches = (barrierMask.value & layerBit) != 0;
            
            Debug.Log($"IsBlocked: Child '{child.name}' - layer={layer}, layerBit={layerBit}, barrierMask={barrierMask.value}, layerMatches={layerMatches}");
            
            if (!layerMatches)
            {
                Debug.Log($"IsBlocked: Child '{child.name}' is not on barrier layer, skipping");
                continue;
            }
            
            string barrierName = col.gameObject.name;
            bool isCellBlockUp = barrierName.Contains("CellBlockUp");
            bool isCellBlockRight = barrierName.Contains("CellBlockRight");
            
            Debug.Log($"IsBlocked: Direct child check - Found '{barrierName}' on cell [{cell.X}, {cell.Y}], isCellBlockUp={isCellBlockUp}, isCellBlockRight={isCellBlockRight}, layer={layer}, enabled={col.enabled}");
            
            // CellBlockUp blocks vertical movement
            // CellBlockUp is positioned at the TOP of a cell, so it blocks:
            // - Movement FROM that cell going up (moving up out of it) - if barrier is on source
            // - Movement TO that cell from above (moving down into it) - if barrier is on destination
            if (isCellBlockUp && !isHorizontalMovement)
            {
                bool isMovingDown = dir.y < 0;
                bool isMovingUp = dir.y > 0;
                
                Debug.Log($"IsBlocked: CellBlockUp check - isMovingDown={isMovingDown}, isMovingUp={isMovingUp}, cell=[{cell.X}, {cell.Y}], otherCell=[{otherCell.X}, {otherCell.Y}], isSourceCell={isSourceCell}");
                
                // CellBlockUp on source blocks upward movement (moving up out of source)
                // CellBlockUp on destination blocks downward movement (moving down into destination)
                if ((isSourceCell && isMovingUp) || (!isSourceCell && isMovingDown))
                {
                    Debug.Log($"IsBlocked: Direct check - CellBlockUp on cell [{cell.X}, {cell.Y}] blocking vertical movement - RETURNING TRUE");
                    return true;
                }
                else
                {
                    Debug.Log($"IsBlocked: CellBlockUp found but not blocking - barrier on [{cell.X}, {cell.Y}], movingDown={isMovingDown}, movingUp={isMovingUp}, isSourceCell={isSourceCell}");
                }
            }
            
            // CellBlockRight blocks horizontal movement
            // CellBlockRight is positioned at the RIGHT of a cell, so it blocks:
            // - Movement FROM that cell going right (moving right out of it) - if barrier is on source
            // - Movement TO that cell from the left (moving right into it) - if barrier is on destination
            if (isCellBlockRight && isHorizontalMovement)
            {
                bool isMovingRight = dir.x > 0;
                bool isMovingLeft = dir.x < 0;
                
                Debug.Log($"IsBlocked: CellBlockRight check - isMovingRight={isMovingRight}, isMovingLeft={isMovingLeft}, cell=[{cell.X}, {cell.Y}], otherCell=[{otherCell.X}, {otherCell.Y}], isSourceCell={isSourceCell}");
                
                // CellBlockRight on source blocks rightward movement (moving right out of source)
                // CellBlockRight on destination blocks leftward movement (moving left into destination)
                if ((isSourceCell && isMovingRight) || (!isSourceCell && isMovingLeft))
                {
                    Debug.Log($"IsBlocked: Direct check - CellBlockRight on cell [{cell.X}, {cell.Y}] blocking horizontal movement - RETURNING TRUE");
                    return true;
                }
                else
                {
                    Debug.Log($"IsBlocked: CellBlockRight found but not blocking - barrier on [{cell.X}, {cell.Y}], movingRight={isMovingRight}, movingLeft={isMovingLeft}, isSourceCell={isSourceCell}");
                }
            }
        }
        
        Debug.Log($"IsBlocked: CheckCellChildrenForBarriers - No blocking barrier found on cell [{cell.X}, {cell.Y}]");
        return false;
    }

    void AddCell(CellView cell)
    {
        path.Add(cell);
        cell.SetVisited(true);

        if (cell.Number != 0)
        {
            visitedNumbers.Push(cell.Number);
            nextNumberExpected = cell.Number + 1;
        }

        RefreshSnakeLine();
    }

    void RemoveLastCell()
    {
        var last = path[^1];
        path.RemoveAt(path.Count - 1);
        last.SetVisited(false);

        if (last.Number != 0)
        {
            if (visitedNumbers.Count > 0 && visitedNumbers.Peek() == last.Number)
                visitedNumbers.Pop();

            nextNumberExpected = last.Number;
        }

        RefreshSnakeLine();
    }

    void ClearPath()
    {
        foreach (var c in path)
            c.SetVisited(false);

        path.Clear();
        visitedNumbers.Clear();
        nextNumberExpected = 2;

        RefreshSnakeLine();
    }


    void RefreshSnakeLine()
    {
        if (snakeLine == null) return;

        snakeLine.positionCount = path.Count;

        for (int i = 0; i < path.Count; i++)
        {
            Vector3 p = path[i].transform.position;
            p.z = snakeZ;
            snakeLine.SetPosition(i, p);
        }

        // Update line color based on game state
        UpdateLineColor();
    }

    void UpdateLineColor()
    {
        if (snakeLine == null) return;

        bool shouldShowRed = ShouldShowRed();

        if (shouldShowRed)
        {
            // Dark red at start, light red at end (where pointer is)
            snakeLine.startColor = new Color(0.5f, 0f, 0f, 1f);  // Dark red
            snakeLine.endColor = new Color(1f, 0.3f, 0.3f, 1f);  // Light red
        }
        else
        {
            // Dark green at start, light green at end (where pointer is)
            snakeLine.startColor = new Color(0f, 0.5f, 0f, 1f);  // Dark green
            snakeLine.endColor = new Color(0.3f, 1f, 0.3f, 1f);  // Light green
        }
    }

    bool ShouldShowRed()
    {
        if (grid == null) return false;

        // Condition (a): Fewer cells than total cells have been visited
        int totalCells = grid.GetGridWidth() * grid.GetGridHeight();
        bool hasUnvisitedCells = path.Count < totalCells;

        // Condition (b): Zero legal forward moves from current position
        bool hasNoLegalMoves = path.Count > 0 && !HasLegalMoves(path[^1]);

        // Show red if both conditions are true
        return hasUnvisitedCells && hasNoLegalMoves;
    }

    bool HasLegalMoves(CellView currentCell)
    {
        if (currentCell == null || grid == null) return false;

        // Get all cells in the grid
        var allCells = grid.GetAllCells();

        // Check each cell to see if it's a valid adjacent move
        foreach (var cell in allCells)
        {
            // Must be adjacent
            if (!IsAdjacent(currentCell, cell))
                continue;

            // Must not be visited
            if (cell.IsVisited)
                continue;

            // Must not be blocked
            if (IsBlocked(currentCell, cell))
                continue;

            // Found at least one legal move
            return true;
        }

        // No legal moves found
        return false;
    }

    bool IsPuzzleSolved()
    {
        if (grid == null) return false;

        // a) Check if all cells are visited and part of the snake
        var allCells = grid.GetAllCells();
        if (allCells.Count == 0) return false;

        foreach (var cell in allCells)
        {
            if (!cell.IsVisited)
            {
                return false; // Not all cells are visited
            }
        }

        // b) Check if snake size equals grid width x height
        int expectedSize = grid.GetGridWidth() * grid.GetGridHeight();
        if (path.Count != expectedSize)
        {
            return false; // Snake doesn't cover all cells
        }

        // c) Check if the list of cell.Numbers is in STRICT ascending order from 1 to highest
        // Extract all non-zero numbers from the path in order
        List<int> numbersInPath = new List<int>();
        foreach (var cell in path)
        {
            if (cell.Number != 0)
            {
                numbersInPath.Add(cell.Number);
            }
        }

        // Must start with 1
        if (numbersInPath.Count == 0 || numbersInPath[0] != 1)
        {
            return false;
        }

        // Must be strictly ascending (each number must be exactly 1 more than the previous)
        for (int i = 1; i < numbersInPath.Count; i++)
        {
            if (numbersInPath[i] != numbersInPath[i - 1] + 1)
            {
                return false; // Not strictly ascending
            }
        }

        // All checks passed - puzzle is solved!
        return true;
    }
}
