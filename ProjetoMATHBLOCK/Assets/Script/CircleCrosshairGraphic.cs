using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Desenha um círculo preenchido OU um anel (círculo "vazado", apenas bordas)
/// diretamente como uma malha de UI, dentro de um Canvas.
/// Usado pelo DynamicCrosshair para animar entre "bolinha" e "anel".
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class CircleCrosshairGraphic : MaskableGraphic
{
    [SerializeField] private float radius = 5f;
    [SerializeField] private float holeRadius = 0f;
    [SerializeField] private int segments = 64;

    /// <summary>
    /// Define o raio externo, o raio do "buraco" (0 = círculo cheio,
    /// maior que 0 = anel) e a cor. Chamado pelo DynamicCrosshair a cada frame.
    /// </summary>
    public void SetShape(float newRadius, float newHoleRadius, Color newColor)
    {
        radius = Mathf.Max(0f, newRadius);
        holeRadius = Mathf.Clamp(newHoleRadius, 0f, Mathf.Max(0f, radius - 0.05f));

        if (color != newColor)
            color = newColor;

        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        if (radius <= 0f || segments < 3)
            return;

        if (holeRadius <= 0.01f)
            DrawFilledCircle(vh);
        else
            DrawRing(vh);
    }

    private void DrawFilledCircle(VertexHelper vh)
    {
        UIVertex centerVert = UIVertex.simpleVert;
        centerVert.color = color;
        centerVert.position = Vector3.zero;
        vh.AddVert(centerVert);

        float angleStep = (2f * Mathf.PI) / segments;

        for (int i = 0; i <= segments; i++)
        {
            float angle = i * angleStep;
            Vector3 pos = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);

            UIVertex v = UIVertex.simpleVert;
            v.color = color;
            v.position = pos;
            vh.AddVert(v);
        }

        for (int i = 1; i <= segments; i++)
        {
            vh.AddTriangle(0, i, i + 1);
        }
    }

    private void DrawRing(VertexHelper vh)
    {
        float angleStep = (2f * Mathf.PI) / segments;

        // Para cada segmento, adiciona um par de vértices: interno (borda do buraco) e externo (borda do anel).
        for (int i = 0; i <= segments; i++)
        {
            float angle = i * angleStep;
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);

            UIVertex inner = UIVertex.simpleVert;
            inner.color = color;
            inner.position = new Vector3(cos * holeRadius, sin * holeRadius, 0f);
            vh.AddVert(inner);

            UIVertex outer = UIVertex.simpleVert;
            outer.color = color;
            outer.position = new Vector3(cos * radius, sin * radius, 0f);
            vh.AddVert(outer);
        }

        for (int i = 0; i < segments; i++)
        {
            int innerCurrent = i * 2;
            int outerCurrent = i * 2 + 1;
            int innerNext = (i + 1) * 2;
            int outerNext = (i + 1) * 2 + 1;

            vh.AddTriangle(innerCurrent, outerCurrent, outerNext);
            vh.AddTriangle(innerCurrent, outerNext, innerNext);
        }
    }
}
