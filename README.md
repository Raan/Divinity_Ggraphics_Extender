[Divinity_Graphics_Extender](https://github.com/Raan/Divinity_Graphics_Extender/releases) — это WPF-приложение на C#, предназначенное для извлечения, просмотра и обратной запаковки графических ассетов (спрайтов) из игр Divine Divinity и Beyond Divinity. Программа работает с внутренними игровыми архивами формата CPackedi.*c и позволяет модифицировать текстуры игры.

### 🚀 Основные возможности

* Чтение игровых архивов: открытие файлов CPackedi.0c, CPackedi.1c и других связанных расширений (.0c ... .9c).
* Просмотр текстур: интерактивный просмотр выбранного спрайта прямо в интерфейсе приложения.
* Сохранение одного конкретного спрайта в формате PNG
* Экспорт диапазона спрайтов по указанным индексам (Min/Max).
* Полная распаковка всего архива в один клик.
* Обратная запаковка (Импорт): сборка измененных PNG-картинок обратно в исходные игровые архивы с поддержкой максимального сжатия.

### 🛠 Требования к модификации графики

При редактировании экспортированных изображений для корректной обратной запаковки необходимо соблюдать строгие правила:
1. 🚫 Не переименовывать файлы изображений (программа ориентируется на ID в названии).
2. 🚫 Не изменять размер (разрешение в пикселях) картинок.
3. 🚫 Не использовать полупрозрачные пиксели (поддерживается только полная прозрачность или непрозрачные цвета).

### 💻 Использование

### 1. Открытие архива

• Нажмите кнопку Open и выберите файл с именем CPackedi (например, CPackedi.0c).
• После загрузки в списке отобразятся заголовки спрайтов с их внутренними параметрами.

### 2. Экспорт (Распаковка)

• Один спрайт: Выберите строку в списке, и изображение появится на экране. Нажмите кнопку сохранения для экспорта в PNG.

• Диапазон спрайтов: Задайте начальный (MinIndex) и конечный (MaxIndex) индексы и экспортируйте выбранный диапазон.

• Весь архив: Нажмите кнопку Save all as PNG. В папке с архивом автоматически создастся новая директория вида ИмяАрхива_Расширение, куда сложатся все изображения.

### 3. Импорт (Запаковка)

• Отредактируйте изображения в созданной папке, соблюдая требования к графике.

• При необходимости отметьте чекбокс максимального сжатия (Max Compress).

• Нажмите кнопку Save PNG to Sprite для создания обновленных файлов архива.

• Помните, что каждый игровой архив состоит из двух файлов, отличающихся одной буквой в названии ("i" или "b"). Переименовывать и перемещать необходимо оба файла.



# Divinity_Graphics_Extender

[Divinity_Graphics_Extender](https://github.com) is a C# WPF application designed for extracting, viewing, and repacking graphical assets (sprites) from the games *Divine Divinity* and *Beyond Divinity*. The program works with internal game archives in the `CPackedi.*c` format and allows modifying game textures.

## 🚀 Key Features

* **Reading game archives:** Opening `CPackedi.0c`, `CPackedi.1c` files, and other related extensions (`.0c` ... `.9c`).
* **Texture viewing:** Interactive preview of a selected sprite directly within the application interface.
* **Saving a single sprite:** Exporting a specific sprite in PNG format.
* **Batch sprite export:** Exporting a range of sprites by specifying minimum and maximum indices (`Min`/`Max`).
* **Full archive extraction:** Unpacking the entire archive with a single click.
* **Repacking (Import):** Reassembling modified PNG images back into the original game archives with maximum compression support.

## 🛠 Graphic Modification Requirements

When editing exported images, you must strictly follow these rules to ensure successful repacking:

1. 🚫 **Do not rename image files** (the program relies on the ID in the file name).
2. 🚫 **Do not change the dimensions** (pixel resolution) of the images.
3. 🚫 **Do not use semi-transparent pixels** (only full transparency or solid colors are supported).

## 💻 Usage

### 1. Opening an Archive
* Click the **Open** button and select a file named `CPackedi` (e.g., `CPackedi.0c`).
* Once loaded, a list of sprite headers along with their internal parameters will be displayed.

### 2. Exporting (Unpacking)
* **Single Sprite:** Select a row in the list to display the image on the screen. Click the save button to export it as a PNG.
* **Sprite Range:** Set the starting (`MinIndex`) and ending (`MaxIndex`) indices, then export the chosen range.
* **Full Archive:** Click the **Save all as PNG** button. A new directory named `ArchiveName_Extension` will be automatically created in the archive's folder, containing all the images.

### 3. Importing (Repacking)
* Edit the images in the created folder while strictly adhering to the graphic requirements.
* If necessary, check the **Max Compress** checkbox.
* Click the **Save PNG to Sprite** button to generate the updated archive files.
* Keep in mind that each game archive consists of two files that differ by just one letter in their names (`i` or `b`). You must rename and move **both** files.
















## ⚖️ Legal Disclaimer / Правовая информация

* **Divine Divinity** and **Beyond Divinity** are registered trademarks of **Larian Studios**.
* All in-game assets, graphics, and data extracted or modified by this tool are the sole property of **Larian Studios**. 
* This project is an unofficial, non-commercial fan tool developed for educational and modding purposes only. It is not endorsed by, affiliated with, or associated with Larian Studios.

---

* Все права на игры **Divine Divinity** и **Beyond Divinity** принадлежат компании **Larian Studios**.
* Игровые ресурсы, графика и данные, извлекаемые или изменяемые данной программой, являются интеллектуальной собственностью **Larian Studios**.
* Данное приложение является неофициальным некоммерческим инструментом для моддинга, создано исключительно в ознакомительных целях и никак не связано с официальными разработчиками.
