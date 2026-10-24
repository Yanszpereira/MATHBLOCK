using UnityEngine;
using UnityEngine.UI;

/// <summary>Reproduz desenhos raster 2D: um quadro inteiro por vez, sem deformacao ou iluminacao 3D.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class PapelDesamassandoUI : MaskableGraphic
{
    public const int QuadrosPorSegundo = 15;
    [SerializeField] private Sprite folha;
    [SerializeField] private Sprite[] quadros = new Sprite[0];
    [SerializeField] private Vector2[] centros = new Vector2[0];
    [SerializeField] private Vector2 tamanhoFinal = Vector2.one;
    private int quadroAtual = -1;
    private int IndiceVisual => quadros.Length > 0 ? (quadroAtual < 0 ? quadros.Length - 1 : Mathf.Min(quadroAtual, quadros.Length - 1)) : -1;
    private Sprite SpriteAtual => IndiceVisual >= 0 ? quadros[IndiceVisual] : folha;
    public override Texture mainTexture => SpriteAtual != null ? SpriteAtual.texture : Texture2D.whiteTexture;
    public int QuantidadeQuadros => Mathf.Max(1, quadros.Length);
    public int QuadroAtual => quadroAtual;

    public void DefinirArte(Sprite sprite) { folha = sprite; SetAllDirty(); }
    public void DefinirQuadros(Sprite[] sprites, Vector2[] pivots, Vector2 areaFinal)
    {
        quadros = sprites; centros = pivots; tamanhoFinal = areaFinal;
        quadroAtual = -1; SetAllDirty();
    }
    public void MostrarQuadro(int indice)
    {
        indice = Mathf.Clamp(indice, 0, QuantidadeQuadros - 1);
        if (quadroAtual == indice) return;
        quadroAtual = indice; SetAllDirty();
    }

    protected override void OnPopulateMesh(VertexHelper malha)
    {
        malha.Clear();
        Sprite sprite = SpriteAtual;
        if (sprite == null) return;
        Rect area = rectTransform.rect, textura = sprite.textureRect;
        if (IndiceVisual >= 0 && centros.Length == quadros.Length)
        {
            // Alinha os desenhos no mesmo centro sem recortar as pontas do papel.
            // A silhueta cresce nos proprios sprites, nao por morph de vertices.
            Vector2 centro = centros[IndiceVisual];
            float largura = area.width / Mathf.Max(.01f, tamanhoFinal.x);
            float altura = area.height / Mathf.Max(.01f, tamanhoFinal.y);
            area = new Rect(-centro.x * largura, -centro.y * altura, largura, altura);
        }
        float u0 = textura.xMin / sprite.texture.width, u1 = textura.xMax / sprite.texture.width;
        float v0 = textura.yMin / sprite.texture.height, v1 = textura.yMax / sprite.texture.height;
        malha.AddVert(new Vector2(area.xMin, area.yMin), color, new Vector2(u0, v0));
        malha.AddVert(new Vector2(area.xMin, area.yMax), color, new Vector2(u0, v1));
        malha.AddVert(new Vector2(area.xMax, area.yMax), color, new Vector2(u1, v1));
        malha.AddVert(new Vector2(area.xMax, area.yMin), color, new Vector2(u1, v0));
        malha.AddTriangle(0, 1, 2); malha.AddTriangle(2, 3, 0);
    }
}
