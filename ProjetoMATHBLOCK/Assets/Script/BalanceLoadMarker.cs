using UnityEngine;

/// <summary>
/// Runtime/debug state attached to each block currently contributing to a scale.
/// The marker deliberately contains no gameplay logic; future UI can read it
/// without depending on the controller's internal graph.
/// </summary>
[DisallowMultipleComponent]
public sealed class BalanceLoadMarker : MonoBehaviour
{
    public enum LoadSide
    {
        Left,
        Right
    }

    [SerializeField] private LoadSide side;
    [SerializeField] private bool directContact;
    [SerializeField] private int contactDistance;
    [SerializeField] private int consideredValue;

    public LoadSide Side => side;
    public bool DirectContact => directContact;
    public bool IndirectContact => !directContact;
    public int ContactDistance => contactDistance;
    public int ConsideredValue => consideredValue;

    public void SetState(LoadSide newSide, bool isDirectContact, int newContactDistance, int value)
    {
        side = newSide;
        directContact = isDirectContact;
        contactDistance = Mathf.Max(0, newContactDistance);
        consideredValue = value;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = side == LoadSide.Left
            ? new Color(0.2f, 0.55f, 1f, 0.85f)
            : new Color(1f, 0.35f, 0.2f, 0.85f);
        Gizmos.DrawWireSphere(transform.position, 0.08f);
    }
}
