using System;
using UnityEngine;

namespace StealthGame
{
    public class Door : MonoBehaviour
    {
        public string KeyName = "key1";

        // Audio procedural: evita repetir el sonido de "cerrada" en cada choque
        private float m_LastLockedSound = -99f;
    
        private void OnCollisionEnter(Collision other)
        {
            PlayerMovement player = other.gameObject.GetComponent<PlayerMovement>();

            if (player == null)
                return;

            if (player.OwnKey(KeyName))
            {
                ProceduralSfx.DoorOpen(transform.position);   // audio procedural
                Destroy(gameObject);
            }
            else if (Time.time - m_LastLockedSound > 1.2f)
            {
                m_LastLockedSound = Time.time;
                ProceduralSfx.DoorLocked(transform.position); // audio procedural
            }
        }
    }
}