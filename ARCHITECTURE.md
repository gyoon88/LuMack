# LuMack 애플리케이션 아키텍처 설명

이 문서는 LuMack 애플리케이션의 아키텍처, 주요 클래스 및 각 구성 요소의 역할을 설명합니다.

## 1. 전체 아키텍처

이 애플리케이션은 **MVVM(Model-View-ViewModel)** 디자인 패턴을 기반으로 구축되었습니다. 이 패턴은 UI(View)와 비즈니스 로직(ViewModel)을 분리하여 코드의 테스트 용이성, 유지보수성 및 재사용성을 높입니다.

- **Model**: 애플리케이션의 데이터와 핵심 비즈니스 로직을 나타냅니다. (`Mask.cs`, `MaskClass.cs`)
- **View**: 사용자에게 보여지는 UI입니다. (`MainWindow.xaml`)
- **ViewModel**: View와 Model 사이의 중재자 역할을 합니다. View를 위한 데이터를 노출하고, View에서 발생하는 사용자 입력을 받아 처리합니다. (`MainViewModel.cs`)

여기에 더해, 코드의 관심사를 분리하고 `MainViewModel`이 비대해지는 것을 방지하기 위해 여러 **서비스(Service)** 클래스를 도입했습니다.

- **Services (`MaskCreationService.cs`)**: 특정 기능(예: 마스크 생성)과 관련된 복잡한 로직을 캡슐화합니다.
- **Utils (`FileService.cs`, `MaskLoaderService.cs`)**: 파일 입출력, 데이터 파싱 등과 같은 유틸리티 기능을 담당합니다.

### 데이터 흐름

1.  **이미지/레시피 로드**:
    -   `View` (`MainWindow`)에서 사용자가 'Open Image' 또는 'Load Recipe' 메뉴를 클릭합니다.
    -   `ViewModel` (`MainViewModel`)에 바인딩된 `ICommand`가 실행됩니다.
    -   `ViewModel`은 `FileService`를 호출하여 파일 열기 대화상자를 표시하고 파일 로드를 처리합니다.
    -   `FileService`는 `MaskLoaderService`를 사용하여 XML 레시피를 파싱하고, 마스크 데이터를 `WriteableBitmap` 이미지로 변환합니다.
    -   이 작업은 **비동기 및 병렬**로 처리되어 UI가 멈추는 것을 방지합니다.
    -   결과로 생성된 `Mask` 객체들이 `MainViewModel`의 `Masks` 컬렉션에 추가됩니다.

2.  **데이터 바인딩**:
    -   `View`는 `MainViewModel`의 `MainImage`와 `Masks` 컬렉션에 데이터 바인딩되어 있습니다.
    -   `Masks` 컬렉션이 변경되면, UI는 자동으로 업데이트되어 이미지 위에 마스크 레이어를 렌더링합니다.

3.  **사용자 상호작용**:
    -   `View` (`MainWindow.xaml.cs`)의 코드 비하인드는 확대/축소/이동과 같은 UI 직접 조작 이벤트를 처리합니다.
    -   마스크 생성(Shift + 클릭) 또는 편집과 같은 데이터 관련 작업은 `ViewModel`의 `ICommand`를 호출하여 처리합니다.

---

## 2. 주요 클래스 및 메서드 설명

### Models

#### `Mask.cs`
- **설명**: 단일 마스크 레이어를 나타내는 데이터 모델입니다.
- **주요 속성**:
    - `Id (Guid)`: 각 마스크의 고유 식별자입니다.
    - `Name (string)`: UI에 표시될 마스크의 이름입니다.
    - `MaskClass (MaskClass?)`: 이 마스크가 속한 클래스(예: "Pad", "Line")입니다. 색상 정보 등을 포함합니다.
    - `IsVisible (bool)`: UI에서 이 마스크의 가시성 여부를 제어합니다.
    - `MaskImage (BitmapSource?)`: 마스크의 시각적 표현입니다. RLE 데이터로부터 렌더링된 반투명 이미지 레이어입니다.

#### `MaskClass.cs`
- **설명**: 마스크를 분류하는 데 사용되는 '클래스'를 정의하는 데이터 모델입니다.
- **주요 속성**:
    - `Name (string)`: 클래스의 이름입니다.
    - `DisplayColor (Color)`: 이 클래스에 속한 마스크가 UI에 표시될 때 사용되는 색상입니다.

### ViewModels

#### `MainViewModel.cs`
- **설명**: 애플리케이션의 메인 ViewModel입니다. View에 필요한 모든 상태와 로직을 관리하는 중앙 허브 역할을 합니다.
- **주요 속성**:
    - `MainImage (ImageSource?)`: 주 이미지 뷰에 표시될 이미지입니다.
    - `Masks (ObservableCollection<Mask>)`: 현재 로드된 모든 마스크 레이어의 컬렉션입니다.
    - `MaskClasses (ObservableCollection<MaskClass>)`: 사용자가 정의하고 선택할 수 있는 모든 마스크 클래스의 컬렉션입니다.
    - `SelectedMask (Mask?)`: 마스크 목록에서 현재 선택된 마스크입니다.
    - `SelectedMaskClass (MaskClass?)`: 클래스 목록에서 현재 선택된 클래스입니다.
    - `StatusText (string)`: 파일 로딩과 같은 장기 실행 작업의 상태를 사용자에게 알리기 위해 상태 표시줄에 표시되는 텍스트입니다.
- **주요 커맨드 (`ICommand`)**:
    - `OpenImageCommand`: `FileService`를 통해 새 이미지를 엽니다.
    - `LoadRecipeCommand`: `FileService`를 통해 XML 마스크 레시피를 엽니다.
    - `CreateMaskFromGVCommand`: `MaskCreationService`를 호출하여 사용자가 클릭한 위치의 Gray Value를 기반으로 새 마스크를 생성합니다.
    - `AddClassCommand`: 새 `MaskClass`를 목록에 추가합니다.
    - `AssignClassCommand`: 선택된 마스크(`SelectedMask`)에 선택된 클래스(`SelectedMaskClass`)를 할당합니다.

#### `RelayCommand.cs`
- **설명**: `ICommand` 인터페이스의 일반적인 구현입니다. ViewModel에서 메서드를 View의 컨트롤(예: 버튼)에 쉽게 바인딩할 수 있게 해줍니다.

### Services & Utils

#### `FileService.cs`
- **설명**: 파일 시스템과의 상호작용을 처리합니다.
- **주요 메서드**:
    - `OpenImage(object sender)`: 사용자가 이미지 파일을 선택할 수 있도록 파일 열기 대화상자를 열고, 선택된 이미지를 `MainViewModel`의 `MainImage` 속성에 로드합니다.
    - `LoadRecipe(object sender)`: XML 레시피 파일을 비동기적으로 로드합니다. `Task.Run`을 사용하여 백그라운드 스레드에서 파일 파싱 및 마스크 생성을 수행하여 UI 응답성을 유지합니다. `MaskLoaderService`를 호출하여 실제 파싱을 수행합니다.

#### `MaskLoaderService.cs`
- **설명**: XML 레시피 파일의 RLE(Run-Length Encoded) 데이터를 파싱하여 실제 마스크 이미지로 변환하는 역할을 담당합니다.
- **주요 메서드**:
    - `LoadMasksFromXml(...)`: XML 문서를 입력받아 포함된 모든 마스크 정의를 읽습니다. 각 마스크에 대해 `WriteableBitmap`을 생성하고, `DrawRleDataOnBitmap`을 호출하여 RLE 데이터를 비트맵에 렌더링합니다. 이 과정은 병렬(`AsParallel()`)로 처리되어 대량의 마스크를 빠르게 로드할 수 있습니다.
    - `DrawRleDataOnBitmap(...)`: `WriteableBitmap`의 픽셀 버퍼에 직접 접근하여 RLE 데이터를 그립니다. 이 저수준 픽셀 조작은 매우 빠르며, 마스크가 주 이미지와 픽셀 단위로 완벽하게 정렬되도록 보장합니다.

#### `MaskCreationService.cs`
- **설명**: 사용자 입력으로부터 새로운 마스크를 생성하는 로직을 캡슐화합니다. `MainViewModel`의 복잡도를 낮추는 역할을 합니다.
- **주요 메서드**:
    - `CreateMaskFromGV(...)`: 사용자가 이미지의 특정 지점을 클릭했을 때 호출됩니다.
    - `FloodFill` 알고리즘을 사용하여 클릭된 지점과 유사한 Gray Value를 가진 인접 픽셀 영역을 찾습니다.
    - `DrawPointsOnBitmap`을 호출하여 찾은 픽셀 영역을 새로운 `WriteableBitmap`에 그려 마스크 이미지를 생성합니다.

### Views

#### `MainWindow.xaml.cs` (Code-Behind)
- **설명**: `MainWindow`의 코드 비하인드 파일입니다. 순수 UI 상호작용(예: 마우스 이벤트 처리)을 담당합니다.
- **주요 기능**:
    - **확대/축소 및 이동**: `MouseWheel`, `MouseMove`, `MouseLeftButtonDown/Up` 이벤트를 처리하여 이미지 캔버스의 `ScaleTransform`과 `TranslateTransform`을 조작합니다.
    - **사용자 입력 전달**: 마우스 클릭 위치와 같은 UI 관련 정보를 `GVCreationParameters` 객체에 담아 `MainViewModel`의 커맨드로 전달합니다.
    - **상태 표시줄 업데이트**: 마우스 위치에 해당하는 픽셀 정보(좌표, RGB, Gray Value)를 계산하여 `MainViewModel`의 속성을 업데이트합니다.
    - **마스크 편집**: 'Edit Mode'일 때 `DrawOnMask` 메서드를 호출하여 선택된 마스크의 `WriteableBitmap`에 직접 그림을 그립니다.
