using UnityEngine;

public class FloorTile : MonoBehaviour
{
    public Color defaultColor;
    public Color hoverColor;
    private Renderer renderer;

    public void Awake(){
        renderer = GetComponent<Renderer>();
        defaultColor = renderer.material.color;
    }

    public void OnMouseEnter(){
        renderer.material.color = hoverColor;
    }

    public void OnMouseExit(){
        renderer.material.color = defaultColor;
    }
}
