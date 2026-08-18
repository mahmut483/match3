using UnityEngine;

public class Node
{
    public bool isUsable;
    public Potion potion;

    public Node(bool _isUsable, Potion _potion)
    {
        isUsable = _isUsable;
        potion = _potion;
    }
}