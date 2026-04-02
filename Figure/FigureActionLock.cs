using System.Collections;
using UnityEngine;

// ============================================================================
// FigureActionLock (per Figure)
//  - Hängt am Figure-Root (oder wird automatisch ergänzt)
//  - Sperrt NUR diese Figur während einer laufenden Aktion (z.B. SpecialMove)
// ============================================================================
[DisallowMultipleComponent]
public class FigureActionLock : MonoBehaviour
{
    [SerializeField] private int _lockCount;

    public bool IsLocked => _lockCount > 0;

    public void Lock() => _lockCount++;

    public void Unlock() => _lockCount = Mathf.Max(0, _lockCount - 1);

    public void ForceUnlockAll() => _lockCount = 0;
}