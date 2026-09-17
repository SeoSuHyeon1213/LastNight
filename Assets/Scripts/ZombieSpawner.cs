using System.Collections.Generic;
using UnityEngine;

// 좀비 게임 오브젝트를 주기적으로 생성
public class ZombieSpawner : MonoBehaviour {
    public Zombie[] zombiePrefab; // 생성할 좀비 원본 프리팹 (메모리에 로딩된 상태)

    public ZombieData[] zombieDatas; // 사용할 좀비 셋업 데이터들
    public Transform[] spawnPoints; // 좀비 AI를 소환할 위치들

    private List<Zombie> zombies = new List<Zombie>(); // 생성된 좀비들을 담는 리스트 (가변형 데이터로 생성 제거에 용이 함)
    //리스트 선언을 위해 인스턴스를 넣는다.
    private int wave; // 현재 웨이브

    private void Update() {
        // 게임 오버 상태일때는 생성하지 않음
        if (GameManager.instance != null && GameManager.instance.isGameover) //게임오버 판정과 함께 gamemanager의 instance를 통해 isgameover를 가져와서 게임오버 상태인지 확인
        {
            return;
        }

        // 좀비를 모두 물리친 경우 다음 스폰 실행
        if (zombies.Count <= 0)
        {
            SpawnWave();
        }

        // UI 갱신
        UpdateUI();
    }

    // 웨이브 정보를 UI로 표시
    private void UpdateUI() {
        // 현재 웨이브와 남은 적 수 표시
        UIManager.instance.UpdateWaveText(wave, zombies.Count); //UIManager의 instance를 통해 UpdateWaveText를 호출하고 매개 변수로 wave와 zombies.Count를 전달하여 UI 갱신
    }

    // 현재 웨이브에 맞춰 좀비들을 생성
    private void SpawnWave() {
        wave++; // 웨이브 증가

        int spawnCount = Mathf.RoundToInt(wave * 1.5f); // 웨이브에 따라 생성할 좀비 수 결정 (웨이브가 올라갈수록 더 많은 좀비 생성)
        
        for (int i = 0; i < spawnCount; i++) {
            CreateZombie(); // 좀비 생성
        }
    }

    // 좀비를 생성하고 생성한 좀비에게 추적할 대상을 할당
    private void CreateZombie() {
        // 사용할 좀비 데이터 랜덤으로 결정
        ZombieData zombieData = zombieDatas[Random.Range(0, zombieDatas.Length)];
        // 생성할 위치를 랜덤으로 결정
        Transform spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];
        // 좀비 프리팹으로부터 좀비 생성
        Zombie zombie = Instantiate(zombiePrefab[Random.Range(0, zombiePrefab.Length)], spawnPoint.position, spawnPoint.rotation);
        zombie.Setup(zombieData); // 생성한 좀비에게 셋업 데이터 전달
        zombies.Add(zombie); // 생성한 좀비를 리스트에 추가
        //람다식으로 onDeath 이벤트에 여러 개의 메서드를 등록
        //anonymous function (람다식)을 사용하여 onDeath 이벤트에 여러 개의 메서드를 등록
        //즉석에서 함수 생성해서 넣기
        zombie.onDeath += () => zombies.Remove(zombie); // 생성된 좀비 리스트에서 제거
        zombie.onDeath += () => Destroy(zombie.gameObject); // 사망한 좀비 10초 뒤에 파괴
        zombie.onDeath += () => GameManager.instance.AddScore(100); // 좀비 사망시 점수 추가
 
    }
}