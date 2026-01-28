using System;
using System.Collections;
using System.Collections.Generic;
using NetGame;
using Unity.VisualScripting;
using UnityEngine;
using Fusion;

[Serializable]
class Wall
{
    public int node1;
    public int node2;
}

public class MazeGenerator : NetworkBehaviour
{
    private NetworkRunner _runner;

    [SerializeField] GameObject mazeCellPrefab;
    [SerializeField] GameObject mazeWallTopPrefab;
    [SerializeField] GameObject mazeWallLeftPrefab;
    [SerializeField] float wallThickness = 3.0f;
    [SerializeField] float mazeCellSize = 15.0f;
    [SerializeField] Vector2Int mazeSize = new Vector2Int(25, 25);
    [SerializeField] Vector2 mazeOffset = new Vector2(0.0f, 0.0f);
    [SerializeField] bool cullWalls = false;
    [SerializeField] Transform mazeParent;

    [SerializeField] float percentWallsRemove;
    [SerializeField] float percentWallsRising;
    [SerializeField] float percentWallsFalling;

    public int randomSeed = 10;

    List<GameObject> cellGOList = new List<GameObject>();
    List<Wall> walls = new List<Wall>();
    List<int> nodeTree = new List<int>();

    void Awake()
    {
        _runner = FindObjectOfType<NetworkRunner>();
    }

    public override void Spawned()
    {
        if (!Runner.IsServer)
            return;

        UnityEngine.Random.InitState(randomSeed);

        GenerateMazeShell();

        // init node tree
        for (int i = 0; i < mazeSize.x * mazeSize.y; i++)
            nodeTree.Add(i);
        // init walls list
        for (int y = 0; y < mazeSize.y; y++)
        {
            for (int x = 0; x < mazeSize.x; x++)
            {
                int current = y * mazeSize.x + x;
                Wall wallLeft = new Wall();
                Wall wallTop = new Wall();

                if (x < mazeSize.x - 1)
                {
                    wallLeft.node1 = current;
                    wallLeft.node2 = current + 1;
                    walls.Add(wallLeft);
                }

                if (y < mazeSize.y - 1)
                {
                    wallTop.node1 = current;
                    wallTop.node2 = current + mazeSize.x;
                    walls.Add(wallTop);
                }
            }
        }
        // shuffle walls list
        for (int i = 0; i < walls.Count; i++)
        {
            int randomIndex = UnityEngine.Random.Range(0, walls.Count);
            Wall temp = walls[i];
            walls[i] = walls[randomIndex];
            walls[randomIndex] = temp;
        }


        GenerateMaze();
        ModifyWalls();

        // generate spawn points
        GameBootstrap gameBootstrap = GameObject.Find("GameBootstrap").GetComponent<GameBootstrap>();
        GameObject spawnPointsObj = GameObject.Find("SpawnPoints");
        for (int i = 0; i < 10; i++)
        {
            GameObject spawnPoint = new GameObject();
            spawnPoint.transform.SetParent(spawnPointsObj.transform);
            spawnPoint.transform.position = cellGOList[UnityEngine.Random.Range(0, cellGOList.Count)].transform.position;
            Vector3 position = spawnPoint.transform.position;
            position.y += 0.75f;
            position.z -= mazeCellSize/2;
            spawnPoint.transform.position = position;
            spawnPoint.transform.name = "sp" + i;
            gameBootstrap.AddSpawnPoint(spawnPoint.transform);
        }
    }

    void GenerateMazeShell()
    {
        if(!_runner.IsServer)
            return;

        cellGOList.Clear();
        // generate maze cells
        for (int y = 0; y < mazeSize.y; y++)
        {
            for (int x = 0; x < mazeSize.x; x++)
            {
                Vector3 cellPose = Vector3.zero;
                cellPose.x = (mazeCellSize - wallThickness) * x + mazeOffset.x;
                cellPose.z = (mazeCellSize - wallThickness) * y + mazeOffset.y;

                NetworkObject cellNO = _runner.Spawn(mazeCellPrefab, cellPose, Quaternion.identity);
                cellNO.transform.SetParent(mazeParent);
                cellGOList.Add(cellNO.gameObject);  
            }   
        }   
        
        // generate maze wall
        for (int y = 0; y < mazeSize.y; y++)
        {
            // top

            Vector3 wallPose = Vector3.zero;
            wallPose.x = -mazeCellSize/2.0f +wallThickness/2.0f;
            wallPose.z = mazeCellSize * y + mazeOffset.y -mazeCellSize/2.0f;
            NetworkObject wall = _runner.Spawn(mazeWallTopPrefab, wallPose, Quaternion.identity);
            wall.transform.SetParent(mazeParent);

            Vector3 wallPose2 = Vector3.zero;
            wallPose2.x = (mazeCellSize - wallThickness)*(mazeSize.x - 1) + mazeOffset.x + mazeCellSize/2.0f - wallThickness/2.0f;
            wallPose2.z = mazeCellSize * y + mazeOffset.y -mazeCellSize/2.0f;
            NetworkObject wall2 = _runner.Spawn(mazeWallTopPrefab, wallPose2, Quaternion.identity);
            wall2.transform.SetParent(mazeParent);
        }
        for (int x = 0; x < mazeSize.x; x++)
        {
            Vector3 wallPose3 = Vector3.zero;
            wallPose3.x = mazeCellSize * x + mazeOffset.x;
            wallPose3.z = (mazeCellSize - wallThickness)*(mazeSize.y - 1) + mazeOffset.y;
            NetworkObject wall3 = _runner.Spawn(mazeWallLeftPrefab, wallPose3, Quaternion.identity);
            wall3.transform.SetParent(mazeParent);

            Vector3 wallPose4 = Vector3.zero;
            wallPose4.x = mazeCellSize * x + mazeOffset.x;
            wallPose4.z = -mazeCellSize/2.0f -wallThickness/2.0f-wallThickness;
            NetworkObject wall4 = _runner.Spawn(mazeWallLeftPrefab, wallPose4, Quaternion.identity);
            wall4.transform.SetParent(mazeParent);
        }
    }

    void GenerateMaze()
    {
        for (int i = 0; i < walls.Count; i++)
        {
            Wall wall = walls[i];
            int root1 = FindRoot(wall.node1);
            int root2 = FindRoot(wall.node2);

            if (root1 != root2)
            {
                nodeTree[FindRoot(wall.node1)] = FindRoot(wall.node2);
                
                // Remove the wall
                
                // check if wall between x axis
                if (wall.node2 - wall.node1 == 1)
                {
                    cellGOList[wall.node2].transform.Find("longZ").gameObject.SetActive(false);
                }
                else
                // wall is between z axis
                {
                    cellGOList[wall.node2].transform.Find("longX").gameObject.SetActive(false);
                }
            }
        }
    }

    int FindRoot(int node)
    {
        int root = node;

        // find root
        while (root != nodeTree[root])
        {
            root = nodeTree[root];
        }
    
        // compress path (make every nodes parent = to found root)
        while (node != root)
        {
            int next = nodeTree[node];
            nodeTree[node] = root;
            node = next;
        }

        return root;
    }

    void ModifyWalls()
    {
        int removeCount = (int)((float)percentWallsRemove * (walls.Count-nodeTree.Count+1));
        int fallingCount = (int)((float)percentWallsFalling * (walls.Count-nodeTree.Count+1));
        int risingCount = (int)((float)percentWallsRising * (walls.Count-nodeTree.Count+1));
     
        int remove_i = 0;
        int fall_i = 0;
        int rise_i = 0;

        Debug.Log(walls.Count);
        Debug.Log(nodeTree.Count);
        Debug.Log(removeCount);
        Debug.Log(fallingCount);
        Debug.Log(risingCount);

        int i = 0;

        for (; i < walls.Count && remove_i < removeCount; i++)
        {
            int node = walls[i].node1;
            GameObject childX = cellGOList[node].transform.Find("longX").gameObject;
            GameObject childZ = cellGOList[node].transform.Find("longZ").gameObject;
            GameObject colorChild = childX;
            if (!colorChild.activeSelf)
            {
                colorChild = childZ;
                if (!colorChild.activeSelf)
                    continue;
            }

            colorChild.SetActive(false);
            remove_i++;
        }

        for (; i < walls.Count && fall_i < fallingCount; i++)
        {
            int node = walls[i].node1;
            bool childXAvailable = cellGOList[node].transform.Find("longX").gameObject.activeSelf;
            bool childZAvailable = cellGOList[node].transform.Find("longZ").gameObject.activeSelf;

            if (childXAvailable)
            {
                cellGOList[node].GetComponent<MazeWallController>().modeX = WallMode.Falling;
                fall_i++;
                continue;
            }

            if (childZAvailable)
            {
                cellGOList[node].GetComponent<MazeWallController>().modeZ = WallMode.Falling;
                fall_i++;
                continue;
            }
        }

        for (; i < walls.Count && rise_i < risingCount; i++)
        {
            int node = walls[i].node1;
            bool childXAvailable = cellGOList[node].transform.Find("longX").gameObject.activeSelf;
            bool childZAvailable = cellGOList[node].transform.Find("longZ").gameObject.activeSelf;

            if (childXAvailable)
            {
                cellGOList[node].GetComponent<MazeWallController>().modeX = WallMode.Rising;
                rise_i++;
                continue;
            }

            if (childZAvailable)
            {
                cellGOList[node].GetComponent<MazeWallController>().modeZ = WallMode.Rising;
                rise_i++;
                continue;
            }
        }
    }
}