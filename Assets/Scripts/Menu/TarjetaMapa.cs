using UnityEngine.UI;

public class TarjetaMapa : Button
{
    public void Init(MapSelector selector, MapSelector.Mapa mapa)
    {
        targetGraphic = GetComponent<Image>();
        interactable = mapa.disponible;
        onClick.AddListener(() => selector.Elegir(mapa));
    }
}
