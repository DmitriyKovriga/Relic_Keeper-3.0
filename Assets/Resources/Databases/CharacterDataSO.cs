using UnityEngine;

[CreateAssetMenu(menuName = "RPG/Character Data")]
public class CharacterDataSO : ScriptableObject
{
    [field: SerializeField] public string ID { get; private set; }
    [field: SerializeField] public string DisplayName { get; private set; }
    
    [Header("Base Stats")]
    public float BaseMaxHealth = 100f;
    public float BaseMaxManna = 50f;
    public float BaseMoveSpeed = 5f;
    public float BaseJumpForce = 12f;
}