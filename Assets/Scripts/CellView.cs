using TMPro;
using UnityEngine;

public class CellView : MonoBehaviour
{
    public int X { get; private set; }
    public int Y { get; private set; }
    public int Number { get; private set; }

    [Header("Optional")]
    public TMP_Text label;

    [Header("Visuals")]
    public SpriteRenderer background;  // assign in prefab
    public SpriteRenderer Badge;  // assign in prefab

    private bool isVisited;
    private static bool hasLoggedBadgeWarning = false;

    void Awake()
    {
        // Try to auto-find Badge if not assigned
        if (Badge == null)
        {
            // Try to find a child GameObject named "Badge" with a SpriteRenderer
            Transform badgeTransform = transform.Find("Badge");
            if (badgeTransform == null)
            {
                // Try recursive search for "Badge"
                badgeTransform = FindChildRecursive(transform, "Badge");
            }
            
            if (badgeTransform != null)
            {
                Badge = badgeTransform.GetComponent<SpriteRenderer>();
            }
        }
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

    public void Init(int x, int y)
    {
        X = x;
        Y = y;
        SetNumber(0);
        SetVisited(false);
    }

    public void SetNumber(int n)
    {
        Number = n;
        if (label != null)
            label.text = (n == 0) ? "" : n.ToString();
        
        // Enable/disable badge based on value
        if (Badge != null)
        {
            bool shouldBeEnabled = n > 0;
            Badge.enabled = shouldBeEnabled;
            
            // Also ensure the GameObject is active (in case it was disabled)
            if (shouldBeEnabled && !Badge.gameObject.activeSelf)
            {
                Badge.gameObject.SetActive(true);
            }
        }
        else if (!hasLoggedBadgeWarning)
        {
            // Debug warning if Badge is not assigned (only log once to avoid spam)
            // Debug.LogWarning($"CellView: Badge SpriteRenderer is not assigned in the Inspector and could not be auto-found! Please assign it in the prefab.");
            hasLoggedBadgeWarning = true;
        }
    }

    public void SetVisited(bool v)
    {
        isVisited = v;

        if (background != null)
        {
            // light blue highlight when visited
            background.color = v ? new Color(0.6f, 0.8f, 1f) : Color.white;
        }
    }

    public bool IsVisited => isVisited;
}
