using System.Collections;
using UnityEngine;

namespace WaterWraithMod.Scripts
{
    public class TireFragments : MonoBehaviour
    {
        public Rigidbody[] Fragments = [];
        public bool P2style = false;
        public void Start()
        {
            if (!P2style)
            {
                Fragments = transform.GetComponentsInChildren<Rigidbody>();

                foreach (var item in Fragments)
                {
                    item.AddExplosionForce(15, transform.position, 7);
                }
            }

            StartCoroutine(RemoveFragments());
        }

        IEnumerator RemoveFragments()
        {
            yield return new WaitForSeconds(30f);

            Destroy(gameObject);
        }
    }
}
