using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [SerializeField]
    private float _moveSpeed = 25;
    private CharacterController _player;

    private void Start()
    {
        _player = GetComponent<CharacterController>();
    }
    
    private void Update()
    {
        var movement = new Vector3(0, 0, -1);
        
        // move the character
        _player?.SimpleMove(movement * Time.deltaTime * _moveSpeed);
    }
}
