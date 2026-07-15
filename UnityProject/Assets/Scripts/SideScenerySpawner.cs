using UnityEngine;

public class SideScenerySpawner : MonoBehaviour
{
    [SerializeField] private GameObject[] prefabs;
    [SerializeField] private float sideOffset = 6f; // clearance from the road edge to the object's near edge
    [SerializeField] private float spawnZ = 70f;
    [SerializeField] private float minInterval = 1.5f;
    [SerializeField] private float maxInterval = 3f;

    private float _timer;
    private float _nextInterval;

    private void Start()
    {
        ScheduleNext();
    }

    private void Update()
    {
        float speed = SpeedController.Instance != null ? SpeedController.Instance.CurrentSpeed : 0f;
        if (speed <= 0f)
            return; // road isn't moving — nothing should appear yet

        _timer += Time.deltaTime;
        if (_timer >= _nextInterval)
        {
            _timer = 0f;
            Spawn();
            ScheduleNext();
        }
    }

    private void ScheduleNext()
    {
        _nextInterval = Random.Range(minInterval, maxInterval);
    }

    private void Spawn()
    {
        if (prefabs == null || prefabs.Length == 0)
            return;

        GameObject prefab = prefabs[Random.Range(0, prefabs.Length)];
        float side = Random.value < 0.5f ? -1f : 1f;

        // Wide sprites (like the pine forest) need more clearance than
        // narrow ones (like the palm tree) or they'll poke onto the road —
        // push out by the object's own half-width on top of the base offset.
        float halfWidth = 0f;
        BoxCollider box = prefab.GetComponent<BoxCollider>();
        if (box != null)
            halfWidth = box.size.x / 2f;

        float x = side * (sideOffset + halfWidth);
        Vector3 pos = new Vector3(x, prefab.transform.position.y, spawnZ);
        Instantiate(prefab, pos, prefab.transform.rotation);
    }
}
