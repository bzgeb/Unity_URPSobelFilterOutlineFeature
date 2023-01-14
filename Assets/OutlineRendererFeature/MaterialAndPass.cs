using UnityEngine;

[System.Serializable]
public class MaterialAndPass
{
    [SerializeField] Material _material;
    [SerializeField] int _index;

    public Material Material => _material;
    public int Index => _index;

    public MaterialAndPass(int index)
    {
        _index = index;
    }
}