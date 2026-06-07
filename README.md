# DEMO ERP — 자동차 제조 공정 통합 관리 시스템

Claude AI를 활용하여 만든 VB.NET WinForms 기반의 자동차 부품 제조 현장용 ERP 시스템입니다.
생소한 자동차 부품 제조업 도메인에 대해서 질문과 검증으로 제작한 ERP 시스템입니다. AI 활용 역량 향상을 목적으로 제작하였습니다.

---

## 실행 방법
- publish/WinFormsApp1 압축 해제
- publish/WinFormsApp1.exe 실행
- id : admin pw:admin123

## AI 활용 방식

- **도메인 학습**: 자동차 제조 도메인의 표준 업무 흐름(백플러시, LOT 추적, 서열 지시, BOM 관리)을 AI를 통해 파악하고 시스템 구조를 설계했습니다.
- **유지보수성 향상**: AI에게 아키텍처 개선 방향을 질문하고 검토한 뒤, 계층형 구조(UI/Services/Repositories 분리)를 적용했습니다.
- **보안 강화**: 보안 취약점을 AI로 검토하여 역할 기반 접근제어와 비밀번호 해싱을 구현했습니다.

---

## 기술 스택

- **언어**: VB.NET (WinForms)
- **프레임워크**: .NET 5
- **데이터베이스**: SQLite 
- **아키텍처**: UI Layer / Service Layer / Repository Layer

---

## 아키텍처

```
UI Layer (Form1.vb, LoginForm.vb, ItemManageForm.vb)
    ↓ 이벤트 처리만 담당, 비즈니스 로직 위임
Service Layer (ProductionService, AuthService, EquipmentService, BarcodeService)
    ↓ 핵심 비즈니스 로직, 트랜잭션 제어
Repository Layer (StockRepository, BomRepository, ProductionRepository, ...)
    ↓ DB 쿼리 전담
DataAccess (DatabaseHelper, SeedDataManager)
    DB 초기화, 스키마 관리, 시드 데이터
```
---

## 핵심 기능

### 1. 생산 시뮬레이터 (7단계 트랜잭션)

```
STEP 1. 설비 인터락 사전 점검
        → 설비 상태/온도/보전 주기 조건 확인, 미충족 시 생산 차단
STEP 2. 공정 시퀀스 실행 (LOT 추적 기록)
        → 프레스 → 용접 → 도장 → 조립 → 검사 순서로 각 공정 LOT 이력 기록
STEP 3. BOM 전개 및 소요량 계산
        → 완제품 기준으로 Multi-Level BOM을 재귀 전개하여 필요 자재량 산출
STEP 4. Poka-Yoke 재고 사전 검증
        → 전 부품 재고가 소요량을 충족하는지 검증, 부족 시 전체 롤백
STEP 5. 생산 실적 기록
        → LOT 번호, 생산 수량, 라인 코드, 작업 일자 기록
STEP 6. 백플러시 자재 차감
        → 완제품 조립 완료 시점에 소요 자재를 일괄 차감, 변동 이력 기록
STEP 7. 완제품 입고
        → 완제품 재고 증가 처리
```

### 2. M5 서열 지시 시뮬레이션

20건의 생산 지시를 연속 처리하며 인터락 차단, 롤백, 트랜잭션 정합성을 검증합니다. 처리 결과(완료/차단 건수, 정합성 %)를 로그로 출력합니다.

### 3. Multi-Level BOM Explorer

완제품 → 반제품 → 원자재로 이어지는 계층 구조를 TreeView로 시각화합니다. BOM을 재귀적으로 전개하여 각 레벨의 소요량을 표시합니다.

### 4. 실시간 재고 대시보드

- 현재 재고와 안전재고를 비교하여 부족 품목을 색상으로 구분(빨강/노랑)
- 생산 실적 KPI(총 조립 건수, 누적 생산량) 실시간 표시
- 품목 더블 클릭 시 재고 변동 이력 팝업

### 5. LOT Traceability

바코드 기반으로 각 LOT의 전 공정 이력을 추적합니다. LOT 더블 클릭 시 프레스부터 검사까지 각 공정의 투입/완료 시각, 품질 결과를 조회할 수 있습니다.

### 6. 설비/라인 모니터링

설비 상태(정상/경고/고장/보전중)를 색상으로 구분하여 표시합니다. 인터락 조건(온도 범위, 보전 주기, 상태 코드)을 DB에서 관리하며 생산 시작 전 자동 점검합니다.

### 7. 부품 및 BOM 관리

신규 품목 등록, 재고 수동 조정(입고/출고/재고조정/반품), BOM 연결 추가/삭제를 UI에서 직접 처리할 수 있습니다.

### 8. 역할 기반 접근제어 (RBAC)

| 역할 | 권한 |
|------|------|
| 관리자 (Admin) | 전체 기능, 데이터 리셋, 품목 삭제 |
| 생산관리자 | 생산 실행, 부품 등록, 재고 조정 |
| 조회전용 | 데이터 조회만 가능 |

비밀번호는 PBKDF2 해싱으로 저장하며, 권한 외 기능 접근 시 예외를 발생시켜 차단합니다.

---

## DB 스키마

```
USER_MASTER        사용자 계정 (PBKDF2 해시 저장)
ITEM_MASTER        품목 마스터 (원자재/반제품/완제품)
BOM_MASTER         BOM 부모-자식 관계 및 소요량
STOCK_MASTER       현재 재고 및 안전재고
STOCK_HISTORY      재고 변동 이력 (BACKFLUSH/RECEIVE/SHIP 등)
PRODUCTION_LOG     생산 실적
PRODUCTION_ORDER   생산 지시 (작업 오더)
LINE_MASTER        생산 라인 정보
EQUIPMENT_MASTER   설비 정보 (온도, 보전 주기, 상태 등)
INTERLOCK_CONDITION 설비 인터락 조건
LOT_TRACE          바코드 기반 공정 이력
```

---

## 실행 방법

```
1. Visual Studio 2022 이상에서 솔루션 열기
2. NuGet 패키지 복원 (System.Data.SQLite 자동 설치)
3. 빌드 후 실행
4. 초기 계정
   - 관리자:      admin / admin123
   - 생산관리자:  manager / manager123
   - 조회전용:    viewer / viewer123
```

---

## 프로젝트 구조

```
kovico_erp_VB/
├── Form1.vb                  메인 폼 (UI 이벤트 처리)
├── LoginForm.vb              로그인 폼
├── ItemManageForm.vb         부품 관리 팝업
├── ApplicationEvents.vb
├── Models/
│   ├── Entities.vb           도메인 엔티티 (UserAccount, BomNode 등)
│   ├── Enums.vb              열거형 (UserRole, ProcessStep, EquipmentStatus 등)
│   └── Exceptions.vb         커스텀 예외 (ErpException, StockShortageException 등)
├── Services/
│   ├── ProductionService.vb  생산 실행 핵심 로직 (7단계 트랜잭션)
│   ├── AuthService.vb        인증/권한 관리
│   ├── EquipmentService.vb   설비 인터락 점검
│   ├── BarcodeService.vb     LOT 번호/바코드 생성
│   └── PasswordHasher.vb     PBKDF2 해싱
└── DataAccess/
    ├── DatabaseHelper.vb     DB 초기화, 스키마, 연결 관리
    ├── Repositories.vb       Stock/Bom/Production/Equipment/Lot Repository
    └── SeedDataManager.vb    초기 데이터 삽입
```
