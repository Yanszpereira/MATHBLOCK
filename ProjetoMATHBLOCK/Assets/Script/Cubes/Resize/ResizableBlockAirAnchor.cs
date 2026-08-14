using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(ResizableBlock), typeof(Rigidbody))]
public sealed class ResizableBlockAirAnchor : MonoBehaviour
{
    [SerializeField] private ResizableBlock resizableBlock;
    [SerializeField] private Rigidbody blockRigidbody;

    private BlockResizeParticleEffect particleEffect;
    private bool isAnchored;

    public bool IsAnchored => isAnchored;
    public BlockResizeParticleEffect ActiveParticleEffect => particleEffect;

    private void Reset()
    {
        ResolveReferences();
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnValidate()
    {
        ResolveReferences();
    }

    private void OnDisable()
    {
        if (isAnchored && Application.isPlaying)
            ReleaseToPhysics();
    }

    private void OnDestroy()
    {
        StopParticles();
    }

    public bool AnchorFromHeldState(Texture2D particleTexture, Color particleColor)
    {
        ResolveReferences();
        if (resizableBlock == null || blockRigidbody == null)
            return false;

        if (isAnchored)
            return true;

        if (!blockRigidbody.isKinematic)
        {
            blockRigidbody.linearVelocity = Vector3.zero;
            blockRigidbody.angularVelocity = Vector3.zero;
        }

        blockRigidbody.useGravity = false;
        blockRigidbody.isKinematic = true;
        isAnchored = true;

        StopParticles();
        particleEffect = BlockResizeParticleEffect.Create(
            resizableBlock,
            particleTexture,
            particleColor
        );
        return true;
    }

    public void ReleaseForGrab()
    {
        if (!isAnchored)
            return;

        isAnchored = false;
        StopParticles();
    }

    public void ReleaseToPhysics()
    {
        if (!isAnchored)
            return;

        isAnchored = false;
        StopParticles();
        ResolveReferences();
        if (blockRigidbody == null)
            return;

        blockRigidbody.isKinematic = false;
        blockRigidbody.useGravity = true;
        blockRigidbody.linearVelocity = Vector3.zero;
        blockRigidbody.angularVelocity = Vector3.zero;
    }

    private void StopParticles()
    {
        if (particleEffect == null)
            return;

        particleEffect.StopAndFadeOut();
        particleEffect = null;
    }

    private void ResolveReferences()
    {
        if (resizableBlock == null)
            resizableBlock = GetComponent<ResizableBlock>();
        if (blockRigidbody == null)
            blockRigidbody = GetComponent<Rigidbody>();
    }
}
