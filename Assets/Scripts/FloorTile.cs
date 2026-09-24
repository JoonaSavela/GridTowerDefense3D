using UnityEngine;

public class FloorTile : MonoBehaviour
{
    public Color defaultColor;
    public Color hoverColor;
    private Renderer tileRenderer;
    private bool hovered;
    private bool hasPlacementTint;
    private Color placementTint;

    public void Awake(){
        tileRenderer = GetComponent<Renderer>();
        defaultColor = tileRenderer.material.color;
    }

    public void OnMouseEnter(){
        hovered = true;
        ApplyColor();
    }

    public void OnMouseExit(){
        hovered = false;
        ApplyColor();
    }

    public void SetPlacementTint(Color color)
    {
        hasPlacementTint = true;
        placementTint = color;
        ApplyColor();
    }

    public void ClearPlacementTint()
    {
        hasPlacementTint = false;
        ApplyColor();
    }

    void ApplyColor()
    {
        if (tileRenderer == null)
            return;

        if (hasPlacementTint)
            tileRenderer.material.color = placementTint;
        else if (hovered)
            tileRenderer.material.color = hoverColor;
        else
            tileRenderer.material.color = defaultColor;
    }
}
