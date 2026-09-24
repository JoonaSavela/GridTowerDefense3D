using UnityEngine;

public class FloorTile : MonoBehaviour
{
    public Color defaultColor;
    public Color hoverColor;
    private Renderer renderer;
    private bool hovered;
    private bool hasPlacementTint;
    private Color placementTint;

    public void Awake(){
        renderer = GetComponent<Renderer>();
        defaultColor = renderer.material.color;
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
        if (renderer == null)
            return;

        if (hasPlacementTint)
            renderer.material.color = placementTint;
        else if (hovered)
            renderer.material.color = hoverColor;
        else
            renderer.material.color = defaultColor;
    }
}
