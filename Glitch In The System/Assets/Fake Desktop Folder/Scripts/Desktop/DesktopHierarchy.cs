using UnityEngine;

public static class DesktopHierarchy
{
    public static void EnsureActive(GameObject root)
    {
        if (root == null) return;

        Transform t = root.transform;
        while (t != null)
        {
            if (!t.gameObject.activeSelf)
                t.gameObject.SetActive(true);
            t = t.parent;
        }
    }
}
