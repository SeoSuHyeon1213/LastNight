using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI; // AI, 내비게이션 시스템 관련 코드를 가져오기

// 좀비 AI 구현 //부모 클래스가 monobehavier 클래스를 상속 받기 때문에 단일 상속 상태 임에도 컴포넌트 기능 사용 가능
public class CrowZombie : Zombie, ISpecialZombie {

    public ZombieData[] zombieDatas; // 좀비 데이터
    public Transform[] spawnPoints; // 좀비 AI를 소환할 위치들
    public Zombie[] zombiePrefab; // 생성할 좀비 원본 프리팹 (메모리에 로딩된 상태)
    private List<Zombie> zombies = new List<Zombie>(); // 생성된 좀비들을 담는 리스트 (가변형 데이터로 생성 제거에 용이 함)

    public void FixedUpdate() {
        // 게임 오버 상태일때는 생성하지 않음
        if (GameManager.instance != null && GameManager.instance.isGameover) //게임오버 판정과 함께 gamemanager의 instance를 통해 isgameover를 가져와서 게임오버 상태인지 확인
        {
            return;
        }

        // 좀비를 모두 물리친 경우 다음 스폰 실행
        if (zombies.Count <= 0)
        {
            SpecialAttack();
        }
    }
    public void SpecialAttack()
    {
        if(dead)
            return;
        // 특수 공격 로직 구현
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
        // 예: 주변에 있는 플레이어에게 추가 피해를 입히는 등의 효과
    }
}
