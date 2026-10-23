using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Lzo64;
using Microsoft.Win32;
using Path = System.IO.Path;
using System.Runtime.InteropServices;

namespace Div_Ggraphics_Extender
{
    public partial class MainWindow : Window
    {
        // Полный путь к открытому файлу индекса спрайтов (.0c, .1c и т.д.)
        private string _pathToImageFile = string.Empty;

        private static readonly LZOCompressor compressor = new LZOCompressor();

        public MainWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Обработчик события смены выбранного спрайта в списке.
        /// Отображает превью выбранного спрайта и обновляет текстовые поля диапазона.
        /// </summary>
        private void LB_Headers_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Защита от срабатывания события при очистке списка
            if (LB_Headers.SelectedIndex < 0) return;

            int number = LB_Headers.SelectedIndex;

            // Формируем пути к индексному файлу и blob-файлу с данными
            string inpDirI = _pathToImageFile;
            string inpDirB = GetAlternatePath(inpDirI, 'b');

            // Загружаем изображение напрямую в компонент превью без промежуточного сохранения на диск
            MyImage.Source = GetSpriteAsWriteableBitmap(inpDirI, inpDirB, number);

            // Автоматически выставляем диапазон сохранения под текущий выбранный спрайт
            TB_MinIndex.Text = number.ToString();
            TB_MaxIndex.Text = number.ToString();
        }

        /// <summary>
        /// Обработчик нажатия кнопки "Open File".
        /// Вызывает диалог выбора игрового архива CPackedi и инициализирует список заголовков.
        /// </summary>
        private void B_OpenFile(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "CPackedi (*.0c...*.12c)|*.0c;*.1c;*.2c;*.3c;*.4c;*.5c;*.6c;*.7c;*.8c;*.9c",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyComputer)
            };

            if (openFileDialog.ShowDialog() != true) return;

            // Валидация: имя файла строго должно быть CPackedi (без учета регистра)
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(openFileDialog.FileName);
            if (!string.Equals(fileNameWithoutExtension, "CPackedi", StringComparison.OrdinalIgnoreCase))
            {
                TB_Path.Text = "Incorrect filename! Expected 'CPackedi'";
                return;
            }

            // Сохраняем путь и обновляем UI
            _pathToImageFile = openFileDialog.FileName;
            TB_Path.Text = _pathToImageFile;
            LB_Headers.Items.Clear();

            // Читаем заголовки спрайтов для построения таблицы в ListBox (теперь это список структур CPackedEntry)
            var headers = GetSpriteEntries(_pathToImageFile);
            int count = 0;

            foreach (var item in headers)
            {
                // Форматируем строку: [Индекс спрайта] | [Ширина] | [Высота] | [Флаги] | [AnchorX] | [AnchorY] | [InnerW] | [InnerH] | [PackedDims]
                string text = $"{count++.ToString().PadRight(6)}|" +
                              $"{item.Width.ToString().PadRight(5)}|" +
                              $"{item.Height.ToString().PadRight(5)}|" +
                              $"{item.Flags.ToString().PadRight(5)}|" +
                              $"{item.AnchorX.ToString().PadRight(5)}|" +
                              $"{item.AnchorY.ToString().PadRight(5)}|" +
                              $"{item.WidthInner.ToString().PadRight(5)}|" +
                              $"{item.HeightInner.ToString().PadRight(5)}|" +
                              $"{item.PackedDims.ToString()}";

                LB_Headers.Items.Add(text);
            }

            // Автоматически выбираем первый элемент и скроллим список на самый верх
            if (LB_Headers.Items.Count > 0)
            {
                LB_Headers.SelectedIndex = 0;
                LB_Headers.ScrollIntoView(LB_Headers.Items[0]);
            }

            // Активируем кнопки управления после успешной загрузки архива
            B_SavePNG.IsEnabled = true;
            B_SaveAllPNG.IsEnabled = true;
            B_SaveSprite.IsEnabled = true;
        }

        /// <summary>
        /// Обработчик нажатия кнопки "Save as PNG".
        /// Сохраняет только выбранный пользователем диапазон спрайтов.
        /// </summary>
        private void B_SaveAsPNG(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_pathToImageFile)) return;

            // Парсим введенный пользователем диапазон индексов (с защитой от некорректного ввода)
            int minID = int.TryParse(TB_MinIndex.Text, out int min) ? min : 0;
            int maxID = int.TryParse(TB_MaxIndex.Text, out int max) ? max : 0;

            // Создаем список идентификаторов для экспорта
            var ids = Enumerable.Range(minID, maxID - minID + 1).ToList();

            try
            {
                string targetFolder = GetTargetFolderName();
                Directory.CreateDirectory(targetFolder);

                string inpDirI = _pathToImageFile;
                string inpDirB = GetAlternatePath(inpDirI, 'b');

                // Передаем список ids, чтобы отфильтровать и сохранить только выбранное
                UnpackToPng(inpDirI, inpDirB, targetFolder, ids);

                MessageBox.Show("Operation completed successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ShowError($"Failed to save sprites: {ex.Message}");
            }
        }

        /// <summary>
        /// Обработчик нажатия кнопки "Save all as PNG".
        /// Полностью распаковывает текущий архив в изображения.
        /// </summary>
        private void B_SaveAllAsPNG(object sender, RoutedEventArgs e)
        {
            try
            {
                string targetFolder = GetTargetFolderName();
                Directory.CreateDirectory(targetFolder);

                string inpDirI = _pathToImageFile;
                string inpDirB = GetAlternatePath(inpDirI, 'b');

                UnpackToPng(inpDirI, inpDirB, targetFolder);

                MessageBox.Show("Operation completed successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ShowError($"Failed to unpack the archive: {ex.Message}");
            }
        }

        /// <summary>
        /// Обработчик нажатия кнопки "Save PNG to Sprite".
        /// Собирает отредактированные PNG картинки обратно в игровой архив (.new файлов).
        /// </summary>
        private void B_SavePNGToSprite(object sender, RoutedEventArgs e)
        {
            try
            {
                string targetFolder = GetTargetFolderName();
                if (!Directory.Exists(targetFolder)) return;

                string inpDirI = _pathToImageFile;
                string inpDirB = GetAlternatePath(inpDirI, 'b');

                // Временные файлы для сохранения сборки
                string newInpDirI = inpDirI + ".new";
                string newInpDirB = inpDirB + ".new";
                bool compressLevel = CB_MaxCompress.IsChecked == true;

                // Запуск процесса упаковки
                PackFromPng(inpDirI, inpDirB, targetFolder, newInpDirI, newInpDirB, compressLevel);

                // Если стоит чекбокс "Replace Original", заменяем старые файлы новыми
                if (CB_ReplaceOriginal.IsChecked == true)
                {
                    File.Delete(inpDirI);
                    File.Delete(inpDirB);
                    File.Move(newInpDirI, inpDirI);
                    File.Move(newInpDirB, inpDirB);
                }

                MessageBox.Show("Operation completed successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ShowError($"Failed to pack sprites: {ex.Message}");
            }
        }

        /// <summary>
        /// Отображает окно "Справка" с инструкцией по использованию программы и дисклеймером прав.
        /// </summary>
        private void B_Help(object sender, RoutedEventArgs e)
        {
            string helpText =
                "This application is designed for unpacking and packing graphical assets for the games *Divine Divinity* and *Beyond Divinity*.\r\n" +
                "It opens archives such as CPackedi.0c, CPackedi.1c, etc.\r\nTo save an individual sprite, select it from the list and click \"Save as PNG.\"\r\n" +
                "To save multiple sprites, enter a range in the designated fields. To save the entire archive, click \"Save all as PNG\" (this will take longer).\r\n" +
                "Images are saved into a folder-created within the same directory as the archive-that bears the same name as the archive itself.\r\n" +
                "You can then edit these images and pack them back into the archive by clicking \"Save PNG to Sprite.\"\r\n" +
                "Note: You must not rename the images, change their dimensions, or use semi-transparent pixels.\r\n\r\n" +
                "Divine Divinity and Beyond Divinity are registered trademarks of Larian Studios.\r\n" +
                "All in -game assets, graphics, and data extracted or modified by this tool are the sole property of Larian Studios.\r\n" +
                "This project is an unofficial, non - commercial fan tool developed for educational and modding purposes only.\r\n" +
                "It is not endorsed by, affiliated with, or associated with Larian Studios.";
            MessageBox.Show(helpText, "Help / Disclaimer", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        /// 
        /// Генерирует путь к целевой папке на основе пути к архиву.
        /// Пример: "C:\Games\CPackedi.0c" -> "C:\Games\CPackedi_0c"
        /// 
        private string GetTargetFolderName()
        {
            string fileDirectory = Path.GetDirectoryName(_pathToImageFile) ?? string.Empty;
            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(_pathToImageFile);
            string extensionWithoutDot = Path.GetExtension(_pathToImageFile).TrimStart('.');
            return Path.Combine(fileDirectory, $"{fileNameWithoutExtension}_{extensionWithoutDot}");
        }

        public string GetAlternatePath(string originalPath, char replaceChar)
        {
            if (originalPath.Length < 5)
                return originalPath;
            int insertPosition = originalPath.Length - 4;
            return originalPath.Remove(insertPosition, 1).Insert(insertPosition, replaceChar.ToString());
        }
        /// 
        /// Унифицированный метод для отображения окон с ошибками.
        /// 
        private static void ShowError(string message)
        {
            MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }



        /// <summary>
        /// Читает и парсит заголовки спрайтов из индексного файла напрямую в список структур <see cref="CPackedEntry"/>.
        /// </summary>
        /// <param name="inpDirI">Полный путь к индексному файлу спрайтов (например, CPackedi.0c).</param>
        /// <returns>Список распарсенных структур заголовков. Если файл не найден или поврежден, возвращает пустой список.</returns>
        public List<CPackedEntry> GetSpriteEntries(string inpDirI)
        {
            List<CPackedEntry> entries = [];
            const int lineLength = 56; // Фиксированный размер одной записи заголовка в байтах (56 байт)

            // Проверка на существование файла
            if (!File.Exists(inpDirI))
            {
                Debug.WriteLine($"{inpDirI} не найден");
                return entries;
            }

            try
            {
                var fileInfo = new FileInfo(inpDirI);
                // Вычисляем общее количество спрайтов в архиве на основе размера файла
                long entryCount = fileInfo.Length / lineLength;

                // Открываем файл в режиме «только чтение», чтобы избежать блокировок
                using (var fs = File.OpenRead(inpDirI))
                using (var br = new BinaryReader(fs))
                {
                    for (long i = 0; i < entryCount; i++)
                    {
                        // Последовательно считываем первые 9 полей
                        var entry = new CPackedEntry
                        {
                            BlobOffset = br.ReadUInt32(),  // Смещение блока данных спрайта в blob-файле
                            Width = br.ReadUInt32(),       // Полная ширина изображения
                            Height = br.ReadUInt32(),      // Полная высота изображения
                            Flags = br.ReadUInt32(),       // Флаги сжатия и типа ассета
                            AnchorX = br.ReadUInt32(),     // Точка привязки (X-координата центра)
                            AnchorY = br.ReadUInt32(),     // Точка привязки (Y-координата центра)
                            WidthInner = br.ReadUInt32(),  // Внутренняя (полезная) ширина без пустых полей
                            HeightInner = br.ReadUInt32(), // Внутренняя (полезная) высота без пустых полей
                            PackedDims = br.ReadUInt32(),  // Упакованные размеры/параметры кадрирования
                            Reserved = new uint[5]         // Инициализируем массив для зарезервированных данных
                        };

                        // Дочитываем оставшуюся часть структуры (размер под Reserved)
                        // 5 полей * 4 байта = 20 байт (суммарно: 9 * 4 + 20 = 56 байт на запись)
                        for (int j = 0; j < 5; j++)
                        {
                            entry.Reserved[j] = br.ReadUInt32();
                        }

                        entries.Add(entry);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Ошибка при парсинге CPackedEntry: {ex.Message}");
            }

            return entries;
        }

        /// <summary>
        /// Структура, описывающая заголовок упакованного спрайта в игровом архиве Divinity.
        /// Занимает ровно 56 байт в бинарном файле.
        /// </summary>
        public struct CPackedEntry
        {
            /// <summary> 
            /// Абсолютное смещение (в байтах) начала сжатых графических данных спрайта 
            /// в парном файле данных архива (CPackedb).
            /// </summary>
            public uint BlobOffset;

            /// <summary> Полная габаритная ширина клетки/спрайта в пикселях. </summary>
            public uint Width;

            /// <summary> Полная габаритная высота клетки/спрайта в пикселях. </summary>
            public uint Height;

            /// <summary> 
            /// Битовые флаги формата. 
            /// bit 0x01 — стандартный спрайт (карта строк и спанов); 
            /// bit 0x10 — специальный или не-графический ассет.
            /// </summary>
            public uint Flags;

            /// <summary> 
            /// Внутренний отступ кодера по оси X. 
            /// Не используется движком при рендеринге (FUN_00556a80 игнорирует это поле). 
            /// Почти всегда равен 2.
            /// </summary>
            public uint AnchorX;

            /// <summary> 
            /// Внутренний отступ кодера по оси Y. 
            /// Не используется движком при рендеринге. Почти всегда равен 0.
            /// </summary>
            public uint AnchorY;

            /// <summary> Математическое значение: Ширина изображения минус 1 (Width - 1). </summary>
            public uint WidthInner;

            /// <summary> Математическое значение: Высота изображения минус 1 (Height - 1). </summary>
            public uint HeightInner;

            /// <summary> 
            /// Упакованные габариты контента. Закодированы как (sub_height &lt;&lt; 16) | sub_width.
            /// Предположительно: ограничивающий прямоугольник (bounding box) видимых пикселей.
            /// </summary>
            public uint PackedDims;

            /// <summary> 
            /// Зарезервированные поля. Блок из 5 значений UInt32. 
            /// Во всех оригинальных файлах игры заполнен нулями.
            /// </summary>
            public uint[] Reserved; // Длина массива должна быть равна 5
        }

        /// <summary>
        /// Структура, описывающая отдельный горизонтальный отрезок (Span) видимых пикселей.
        /// Используется при RLE-подобном сжатии строк спрайта для пропуска прозрачных пикселей.
        /// </summary>
        public struct Span
        {
            /// <summary> Начальная X-координата текущего видимого отрезка в строке. </summary>
            public ushort StartX;

            /// <summary> Количество идущих подряд пикселей в этом отрезке. </summary>
            public ushort Length;
        }
        /// <summary>
        /// Распаковывает спрайты из игрового архива (индекс + blob-данные) и сохраняет их в формате PNG.
        /// Поддерживает как полную распаковку, так и извлечение строго заданных по индексу спрайтов.
        /// </summary>
        /// <param name="indexFile">Полный путь к индексному файлу (например, CPackedi.0c).</param>
        /// <param name="blobFile">Полный путь к файлу данных (например, CPackedb.0c).</param>
        /// <param name="outputFolder">Путь к целевой папке, куда будут сохранены PNG-изображения.</param>
        /// <param name="spriteIndices">Необязательный список индексов спрайтов. Если пуст или null, извлекаются все изображения.</param>
        public void UnpackToPng(string indexFile, string blobFile, string outputFolder, IReadOnlyCollection<int>? spriteIndices = null)
        {
            // Считываем весь индексный файл в память для быстрого парсинга метаданных
            byte[] idxBytes = File.ReadAllBytes(indexFile);
            int entryCount = idxBytes.Length / 56; // Каждая запись индекса CPacked занимает ровно 56 байт

            List<CPackedEntry> entries = new List<CPackedEntry>();

            // Шаг 1. Читаем структуру заголовков всех спрайтов из индексного файла
            using (BinaryReader idxReader = new BinaryReader(new MemoryStream(idxBytes)))
            {
                for (int i = 0; i < entryCount; i++)
                {
                    CPackedEntry entry = new CPackedEntry();
                    entry.BlobOffset = idxReader.ReadUInt32(); // Смещение начала LZO-блока в blob-файле
                    entry.Width = idxReader.ReadUInt32();      // Полная ширина холста
                    entry.Height = idxReader.ReadUInt32();     // Полная высота холста
                    entry.Flags = idxReader.ReadUInt32();      // Битовые флаги типа ассета
                    entry.AnchorX = idxReader.ReadUInt32();    // Внутренний отступ кодера по X
                    entry.AnchorY = idxReader.ReadUInt32();    // Внутренний отступ кодера по Y
                    entry.WidthInner = idxReader.ReadUInt32(); // На самом деле Width - 1 
                    entry.HeightInner = idxReader.ReadUInt32();// На самом деле Height - 1 
                    entry.PackedDims = idxReader.ReadUInt32(); // Упакованные габариты контента (sub_height << 16) | sub_width

                    // Чтение 5 зарезервированных полей u32 (в оригинальных архивах всегда заполнены нулями)
                    entry.Reserved = new uint[5];
                    for (int j = 0; j < 5; j++) entry.Reserved[j] = idxReader.ReadUInt32();

                    entries.Add(entry);
                }
            }

            // Подготовка хэш-сета для поиска
            bool filterByIndex = spriteIndices != null && spriteIndices.Count > 0;
            HashSet<int>? allowedIndices = filterByIndex ? new HashSet<int>(spriteIndices!) : null;

            // Шаг 2. Последовательно читаем графические блоки из blob-файла
            using (FileStream blobFs = File.OpenRead(blobFile))
            using (BinaryReader blobReader = new BinaryReader(blobFs))
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    // ЕСЛИ включен фильтр И текущего индекса 'i' нет в списке разрешенных -> пропускаем итерацию
                    if (filterByIndex && !allowedIndices!.Contains(i))
                        continue;

                    var entry = entries[i];

                    // Фильтрация ассетов: пропускаем специальные/не-графические данные (Flags & 0x10) и палитренные спрайты (Flags & 0x04)
                    if ((entry.Flags & 0x10) != 0 || (entry.Flags & 0x04) != 0)
                        continue;

                    // ВЕТКА А: Стандартный разреженный спрайт с таблицей строк и спанов (Flags & 0x01)
                    if ((entry.Flags & 0x01) != 0)
                    {
                        // Переходим к началу сжатого LZO-блока
                        blobFs.Position = entry.BlobOffset;
                        uint uncompressedSize = blobReader.ReadUInt32(); // Размер после разжатия

                        // Вычисляем размер сжатого потока на основе смещения следующего элемента
                        long nextOffset = (i + 1 < entries.Count) ? entries[i + 1].BlobOffset : blobFs.Length;
                        int compressedSize = (int)(nextOffset - entry.BlobOffset - 4);

                        // Считываем чистый сжатый поток LZO1X-1 из файла игры
                        byte[] gameLzoStream = blobReader.ReadBytes(compressedSize);

                        // Подготавливаем массив для декомпрессора
                        byte[] lzoInputForYourMethod = new byte[gameLzoStream.Length + 4];
                        Array.Copy(gameLzoStream, 0, lzoInputForYourMethod, 0, gameLzoStream.Length);
                        byte[] lenBytes = BitConverter.GetBytes(uncompressedSize);
                        Array.Copy(lenBytes, 0, lzoInputForYourMethod, gameLzoStream.Length, 4);

                        // Разжимаем блок данных в Payload
                        byte[] payload = compressor.Decompress(lzoInputForYourMethod);

                        // Читаем внутренний заголовок разжатого Payload спрайта
                        uint totalSize = BitConverter.ToUInt32(payload, 0);
                        uint pixelDataOffset = BitConverter.ToUInt32(payload, 4);  // Смещение массива пикселей RGB565
                        ushort width = BitConverter.ToUInt16(payload, 8);          // Реальная ширина
                        ushort height = BitConverter.ToUInt16(payload, 10);        // Реальная высота (число строк)

                        // Создаем пустое изображение в System.Drawing
                        Bitmap bmp = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                        // Выделяем буфер под пиксели всего изображения в формате BGRA (4 байта на пиксель)
                        // По умолчанию массив заполнен нулями (0,0,0,0), что соответствует полностью прозрачному пикселю
                        byte[] argbBuffer = new byte[width * height * 4];

                        int cursor = 12; // Смещение начала таблицы строк в Payload
                        for (int y = 0; y < height; y++)
                        {
                            // Читаем количество видимых отрезков (спанов) на текущей строке
                            ushort numSpans = BitConverter.ToUInt16(payload, cursor);
                            if (numSpans == 0)
                            {
                                // Пропускаем пустую строку
                                cursor += 8;
                                continue;
                            }

                            // Читаем смещение пикселей строки (учитывая намеренное бинарное смещение +2 байта)
                            uint pixelOffset = BitConverter.ToUInt32(payload, cursor + 2);
                            int spansStart = cursor + 6;
                            int pxCursor = (int)(pixelDataOffset + pixelOffset);

                            // Проходим по каждому непрозрачному отрезку в строке
                            for (int s = 0; s < numSpans; s++)
                            {
                                int spanOffset = spansStart + (s * 4);
                                ushort startX = BitConverter.ToUInt16(payload, spanOffset); // Стартовая X координата спана
                                ushort length = BitConverter.ToUInt16(payload, spanOffset + 2); // Длина спана

                                // Вычисляем базовый индекс в байтовом массиве для начала этого отрезка в строке 'y'
                                int bufferDataIndex = (y * width + startX) * 4;

                                // Декодируем пиксели спана напрямую в массив байт
                                for (int x = 0; x < length; x++)
                                {
                                    ushort rgb565 = BitConverter.ToUInt16(payload, pxCursor);
                                    pxCursor += 2;

                                    // Конвертация 16-бит цвета в 24-бит (RGB888)
                                    int r = ((rgb565 >> 11) & 0x1F) * 255 / 31;
                                    int g = ((rgb565 >> 5) & 0x3F) * 255 / 63;
                                    int b = (rgb565 & 0x1F) * 255 / 31;

                                    // Записываем пиксель в формате BGRA
                                    argbBuffer[bufferDataIndex] = (byte)b;         // B
                                    argbBuffer[bufferDataIndex + 1] = (byte)g;     // G
                                    argbBuffer[bufferDataIndex + 2] = (byte)r;     // R
                                    argbBuffer[bufferDataIndex + 3] = 255;         // A (Альфа-канал видимого пикселя)

                                    bufferDataIndex += 4; // Смещаемся к следующему пикселю в буфере
                                }
                            }
                            // Сдвигаем курсор Payload к следующей строке
                            cursor += (numSpans + 2) * 4;
                        }

                        // Быстрая выгрузка заполненного массива байт в Bitmap через LockBits
                        Rectangle rect = new Rectangle(0, 0, width, height);
                        BitmapData bmpData = bmp.LockBits(rect, ImageLockMode.WriteOnly, bmp.PixelFormat);

                        // Копируем всю структуру пикселей за один системный вызов
                        System.Runtime.InteropServices.Marshal.Copy(argbBuffer, 0, bmpData.Scan0, argbBuffer.Length);

                        bmp.UnlockBits(bmpData);

                        // Сохраняем готовый спрайт объектов на диск
                        bmp.Save(Path.Combine(outputFolder, $"{i}.png"), ImageFormat.Png);
                        bmp.Dispose();
                    }
                    // ВЕТКА Б: Сырой фиксированный текстурный ассет (например, текстура ландшафта 64x64 без карты спанов)
                    else
                    {
                        blobFs.Position = entry.BlobOffset;
                        uint uncompressedSize = blobReader.ReadUInt32();

                        long nextOffset = (i + 1 < entries.Count) ? entries[i + 1].BlobOffset : blobFs.Length;
                        int compressedSize = (int)(nextOffset - entry.BlobOffset - 4);

                        byte[] gameLzoStream = blobReader.ReadBytes(compressedSize);

                        byte[] lzoInputForYourMethod = new byte[gameLzoStream.Length + 4];
                        Array.Copy(gameLzoStream, 0, lzoInputForYourMethod, 0, gameLzoStream.Length);
                        byte[] lenBytes = BitConverter.GetBytes(uncompressedSize);
                        Array.Copy(lenBytes, 0, lzoInputForYourMethod, gameLzoStream.Length, 4);

                        byte[] payload = compressor.Decompress(lzoInputForYourMethod);

                        // Для данной ветки флагов ассеты всегда представляют собой сплошную сетку пикселей 64x64
                        const int width = 64;
                        const int height = 64;

                        Bitmap bmp = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                        // Выделяем массив под итоговые пиксели в формате 32bpp ARGB (4 байта на пиксель)
                        byte[] argbBuffer = new byte[width * height * 4];

                        int pxCursor = 0;   // Указатель на исходный поток RGB565 (2 байта на пиксель)
                        int bufferData = 0; // Указатель на целевой массив ARGB (4 байта на пиксель)

                        // Декодируем весь блок пикселей в буфер памяти
                        for (int y = 0; y < height; y++)
                        {
                            for (int x = 0; x < width; x++)
                            {
                                ushort rgb565 = BitConverter.ToUInt16(payload, pxCursor);
                                pxCursor += 2;

                                // Преобразование цвета 5-6-5 в стандартную 8-битную палитру
                                int r = ((rgb565 >> 11) & 0x1F) * 255 / 31;
                                int g = ((rgb565 >> 5) & 0x3F) * 255 / 63;
                                int b = (rgb565 & 0x1F) * 255 / 31;

                                // В памяти Format32bppArgb пиксели лежат побайтово в порядке BGRA
                                argbBuffer[bufferData] = (byte)b;         // Blue
                                argbBuffer[bufferData + 1] = (byte)g;     // Green
                                argbBuffer[bufferData + 2] = (byte)r;     // Red
                                argbBuffer[bufferData + 3] = 255;         // Alpha (Текстуры земли полностью непрозрачны)

                                bufferData += 4;
                            }
                        }

                        // Блокируем память Bitmap в монопольный доступ для быстрой записи
                        Rectangle rect = new Rectangle(0, 0, width, height);
                        BitmapData bmpData = bmp.LockBits(rect, ImageLockMode.WriteOnly, bmp.PixelFormat);

                        // Копируем весь массив байт за один проход на уровне ядра напрямую в структуру Bitmap
                        System.Runtime.InteropServices.Marshal.Copy(argbBuffer, 0, bmpData.Scan0, argbBuffer.Length);

                        // Разблокируем Bitmap, возвращая его системе
                        bmp.UnlockBits(bmpData);

                        // Сохраняем готовую текстуру на диск
                        bmp.Save(Path.Combine(outputFolder, $"{i}.png"), ImageFormat.Png);
                        bmp.Dispose();
                    }
                }
            }
        }
        public void PackFromPng(string originalIndexFile, string originalBlobFile, string pngFolder, string outputIndexFile, string outputBlobFile, bool maxCompress)
        {
            byte[] idxBytes = File.ReadAllBytes(originalIndexFile);
            int entryCount = idxBytes.Length / 56;

            List<CPackedEntry> entries = new List<CPackedEntry>();
            using (BinaryReader idxReader = new BinaryReader(new MemoryStream(idxBytes)))
            {
                for (int i = 0; i < entryCount; i++)
                {
                    CPackedEntry entry = new CPackedEntry
                    {
                        BlobOffset = idxReader.ReadUInt32(),
                        Width = idxReader.ReadUInt32(),
                        Height = idxReader.ReadUInt32(),
                        Flags = idxReader.ReadUInt32(),
                        AnchorX = idxReader.ReadUInt32(),
                        AnchorY = idxReader.ReadUInt32(),
                        WidthInner = idxReader.ReadUInt32(),
                        HeightInner = idxReader.ReadUInt32(),
                        PackedDims = idxReader.ReadUInt32(),
                        Reserved = new uint[5]
                    };
                    for (int j = 0; j < 5; j++) entry.Reserved[j] = idxReader.ReadUInt32();
                    entries.Add(entry);
                }
            }

            // Открываем оригинальный blob для чтения неизмененных данных
            using (FileStream srcBlobFs = File.OpenRead(originalBlobFile))
            using (BinaryReader srcBlobReader = new BinaryReader(srcBlobFs))
            // Создаем новые файлы индекса и блоба
            using (FileStream outBlobFs = File.Create(outputBlobFile))
            using (BinaryWriter blobWriter = new BinaryWriter(outBlobFs))
            using (FileStream outIdxFs = File.Create(outputIndexFile))
            using (BinaryWriter idxWriter = new BinaryWriter(outIdxFs))
            {
                uint currentBlobOffset = 0;

                for (int i = 0; i < entries.Count; i++)
                {
                    var entry = entries[i];
                    string pngPath = Path.Combine(pngFolder, $"{i}.png");

                    // Рассчитываем размер оригинального сжатого блока (вместе с u32 uncompressed_size заголовком)
                    long nextOffset = (i + 1 < entries.Count) ? entries[i + 1].BlobOffset : srcBlobFs.Length;
                    int totalOriginalBlockSize = (int)(nextOffset - entry.BlobOffset);

                    // Исключаем неподдерживаемые утилитой форматы (palette, special) 
                    // ИЛИ проверяем, создал ли пользователь измененный PNG-файл.
                    // Если файла нет, просто копируем оригинальный блок "один в один".
                    if ((entry.Flags & 0x10) != 0 || (entry.Flags & 0x04) != 0 || !File.Exists(pngPath))
                    {
                        srcBlobFs.Position = entry.BlobOffset;
                        byte[] rawBlock = srcBlobReader.ReadBytes(totalOriginalBlockSize);

                        // Пишем в новый blob "как есть"
                        blobWriter.Write(rawBlock);

                        // Обновляем смещение и сохраняем элемент индекса
                        entry.BlobOffset = currentBlobOffset;
                        WriteEntryToIdx(idxWriter, entry);

                        currentBlobOffset += (uint)totalOriginalBlockSize;
                        continue;
                    }

                    // --- ОБРАБОТКА ИЗМЕНЕННЫХ ИЗОБРАЖЕНИЙ ПОЛЬЗОВАТЕЛЯ ---
                    byte[] cellPayload;

                    if ((entry.Flags & 0x01) != 0)
                    {
                        // Ветка Flags & 0x01 (Кастомный Span Table спрайт с прозрачностью)
                        cellPayload = BuildStandardCellPayload(pngPath);
                    }
                    else
                    {
                        // Ветка else (Сырой тайл 64х64)
                        cellPayload = BuildRaw64CellPayload(pngPath);
                    }

                    byte[] compressedWithTail = [];
                    // Вызываем компрессор
                    if (maxCompress)
                    {
                        compressedWithTail = compressor.Compress_999(cellPayload);
                    }
                    else
                    {
                        compressedWithTail = compressor.Compress_1(cellPayload);
                    }
                    // Отрезаем последние 4 байта (хвост длины, который добавляет ваш компрессор)
                    int pureLzoLength = compressedWithTail.Length - 4;
                    byte[] pureLzoStream = new byte[pureLzoLength];
                    Array.Copy(compressedWithTail, 0, pureLzoStream, 0, pureLzoLength);

                    // Записываем обновленные данные в новый Blob
                    entry.BlobOffset = currentBlobOffset;
                    blobWriter.Write((uint)cellPayload.Length); // Сначала u32 размер несжатых данных
                    blobWriter.Write(pureLzoStream);           // Затем чистый LZO поток

                    // Пишем обновленный элемент в индекс
                    WriteEntryToIdx(idxWriter, entry);

                    // Сдвигаем смещение: 4 байта заголовка + длина сжатого стрима
                    currentBlobOffset += 4 + (uint)pureLzoLength;
                }
            }
        }
        /// <summary>
        /// Запаковка для Ветки else (текстуры ландшафта): Формирует плоский (raw) поток пикселей 64x64.
        /// </summary>
        private byte[] BuildRaw64CellPayload(string pngPath)
        {
            const int targetWidth = 64;
            const int targetHeight = 64;
            byte[] payload = new byte[targetWidth * targetHeight * 2]; // 64x64 пикселя, по 2 байта на пиксель (RGB565)
            int pxCursor = 0;

            using (Bitmap bmp = new Bitmap(pngPath))
            {
                // Блокируем пиксели в формате 32bpp ARGB (в памяти это структура BGRA)
                BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                byte[] argbBuffer = new byte[bmp.Width * bmp.Height * 4];
                Marshal.Copy(bmpData.Scan0, argbBuffer, 0, argbBuffer.Length);
                bmp.UnlockBits(bmpData);

                // Принудительно генерируем сетку 64x64, даже если размер исходного PNG отличается
                for (int y = 0; y < targetHeight; y++)
                {
                    for (int x = 0; x < targetWidth; x++)
                    {
                        int r = 0, g = 0, b = 0;

                        // Защита от выхода за границы: если пиксель есть в исходном PNG, берем его цвета
                        if (x < bmp.Width && y < bmp.Height)
                        {
                            int argbIndex = (y * bmp.Width + x) * 4;
                            b = argbBuffer[argbIndex];
                            g = argbBuffer[argbIndex + 1];
                            r = argbBuffer[argbIndex + 2];
                        }

                        // Конвертация RGB888 -> RGB565 (5 бит красный, 6 бит зеленый, 5 бит синий)
                        ushort r5 = (ushort)((r * 31 / 255) << 11);
                        ushort g6 = (ushort)((g * 63 / 255) << 5);
                        ushort b5 = (ushort)(b * 31 / 255);
                        ushort rgb565 = (ushort)(r5 | g6 | b5);

                        // Записываем 2 байта в итоговый Payload (Little-Endian)
                        payload[pxCursor] = (byte)(rgb565 & 0xFF);
                        payload[pxCursor + 1] = (byte)((rgb565 >> 8) & 0xFF);
                        pxCursor += 2;
                    }
                }
            }
            return payload;
        }

        /// <summary>
        /// Запаковка для Ветки if (Flags &amp; 0x01): Генерация разреженного Span Table спрайта.
        /// </summary>
        private byte[] BuildStandardCellPayload(string pngPath)
        {
            using (Bitmap bmp = new Bitmap(pngPath))
            {
                ushort width = (ushort)bmp.Width;
                ushort height = (ushort)bmp.Height;

                // Блокируем пиксели исходного PNG
                BitmapData bmpData = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                byte[] argbBuffer = new byte[width * height * 4];
                Marshal.Copy(bmpData.Scan0, argbBuffer, 0, argbBuffer.Length);
                bmp.UnlockBits(bmpData);

                // Списки для накопления динамических структур строк и пикселей
                List<byte> lineTableBytes = new List<byte>();
                List<byte> pixelBytes = new List<byte>();
                uint currentPixelOffset = 0;

                // Временные структуры для расчета размеров
                var allSpansPerLine = new List<List<Span>>(height);
                int totalLineTableSize = 0;

                // Шаг 1: Сканируем пиксели, выделяем видимые спаны и сразу считаем точный размер таблицы строк
                for (int y = 0; y < height; y++)
                {
                    var spans = GetSpansFromRowOptimized(argbBuffer, y, width);
                    allSpansPerLine.Add(spans);

                    if (spans.Count == 0)
                        totalLineTableSize += 8; // Пустая строка занимает ровно 8 байт
                    else
                        totalLineTableSize += (spans.Count + 2) * 4; // Формула stride: (n + 2) * 4 байт
                }

                // Вычисляем абсолютное смещение начала пиксельных данных в Payload
                uint pixelDataOffset = (uint)(12 + totalLineTableSize);

                // Шаг 2: Формируем бинарные буферы таблицы строк и пикселей на основе собранных спанов
                for (int y = 0; y < height; y++)
                {
                    var spans = allSpansPerLine[y];

                    // Если строка пустая (полностью прозрачная)
                    if (spans.Count == 0)
                    {
                        lineTableBytes.AddRange(BitConverter.GetBytes((ushort)0)); // num_spans = 0
                        lineTableBytes.AddRange(BitConverter.GetBytes((ushort)0)); // padding
                        lineTableBytes.AddRange(BitConverter.GetBytes((uint)0));   // pixel_offset = 0
                        continue;
                    }

                    // Записываем количество спанов в строке
                    lineTableBytes.AddRange(BitConverter.GetBytes((ushort)spans.Count));

                    // Намеренное смещение по байту cursor + 2 игры для относительного pixel_offset
                    lineTableBytes.AddRange(BitConverter.GetBytes(currentPixelOffset));

                    // Записываем каждый спан текущей строки и кодируем его пиксели
                    foreach (var span in spans)
                    {
                        lineTableBytes.AddRange(BitConverter.GetBytes(span.StartX));
                        lineTableBytes.AddRange(BitConverter.GetBytes(span.Length));

                        int rowBaseIndex = y * width * 4;

                        for (int x = 0; x < span.Length; x++)
                        {
                            int argbIndex = rowBaseIndex + (span.StartX + x) * 4;
                            byte b = argbBuffer[argbIndex];
                            byte g = argbBuffer[argbIndex + 1];
                            byte r = argbBuffer[argbIndex + 2];

                            // Конвертация в игровой формат RGB565
                            ushort r5 = (ushort)((r * 31 / 255) << 11);
                            ushort g6 = (ushort)((g * 63 / 255) << 5);
                            ushort b5 = (ushort)(b * 31 / 255);
                            ushort rgb565 = (ushort)(r5 | g6 | b5);

                            pixelBytes.AddRange(BitConverter.GetBytes(rgb565));
                            currentPixelOffset += 2; // Каждый пиксель RGB565 занимает 2 байта
                        }
                    }

                    // Выравнивающий двухбайтовый pad (0) в конце непустой scan-линии
                    lineTableBytes.AddRange(BitConverter.GetBytes((ushort)0));
                }

                // Шаг 3: Собираем заголовок разжатого Payload и объединяем все потоки
                uint totalPayloadSize = (uint)(12 + lineTableBytes.Count + pixelBytes.Count);
                byte[] payloadResult = new byte[totalPayloadSize];

                // Запись главного 12-байтового заголовка Payload
                Buffer.BlockCopy(BitConverter.GetBytes(totalPayloadSize), 0, payloadResult, 0, 4);
                Buffer.BlockCopy(BitConverter.GetBytes(pixelDataOffset), 0, payloadResult, 4, 4);
                Buffer.BlockCopy(BitConverter.GetBytes(width), 0, payloadResult, 8, 2);
                Buffer.BlockCopy(BitConverter.GetBytes(height), 0, payloadResult, 10, 2);

                // Копируем сгенерированную таблицу строк и пиксельные данные в единый результирующий массив
                lineTableBytes.CopyTo(payloadResult, 12);
                pixelBytes.CopyTo(payloadResult, (int)pixelDataOffset);

                return payloadResult;
            }
        }
        /// <summary>
        /// Высокоскоростное сканирование строки буфера пикселей для обнаружения непрозрачных спанов.
        /// </summary>
        private List<Span> GetSpansFromRowOptimized(byte[] argbBuffer, int y, int width)
        {
            List<Span> spans = new List<Span>();
            bool inSpan = false;
            ushort startX = 0;
            int rowBaseIndex = y * width * 4;

            for (ushort x = 0; x < width; x++)
            {
                // Извлекаем альфа-канал пикселя из массива BGRA структуры
                byte alpha = argbBuffer[rowBaseIndex + (x * 4) + 3];

                // В игре используется бинарная маска прозрачности (считаем видимым при Alpha >= 1)
                bool isOpaque = alpha > 0;

                if (isOpaque && !inSpan)
                {
                    inSpan = true;
                    startX = x;
                }
                else if (!isOpaque && inSpan)
                {
                    spans.Add(new Span { StartX = startX, Length = (ushort)(x - startX) });
                    inSpan = false;
                }
            }

            // Если строка завершилась на непрозрачном пикселе, закрываем спан принудительно
            if (inSpan)
            {
                spans.Add(new Span { StartX = startX, Length = (ushort)(width - startX) });
            }

            return spans;
        }

        /// <summary>
        /// Записывает измененную или скопированную структуру CPackedEntry в бинарный поток индексного файла.
        /// </summary>
        private void WriteEntryToIdx(BinaryWriter idxWriter, CPackedEntry entry)
        {
            idxWriter.Write(entry.BlobOffset);
            idxWriter.Write(entry.Width);
            idxWriter.Write(entry.Height);
            idxWriter.Write(entry.Flags);
            idxWriter.Write(entry.AnchorX);
            idxWriter.Write(entry.AnchorY);
            idxWriter.Write(entry.WidthInner);  // По спецификации: Width - 1
            idxWriter.Write(entry.HeightInner); // По спецификации: Height - 1
            idxWriter.Write(entry.PackedDims);  // Упакованные габариты: (height << 16) | width

            for (int j = 0; j < 5; j++)
            {
                idxWriter.Write(entry.Reserved[j]);
            }
        }
        public WriteableBitmap GetSpriteAsWriteableBitmap(string indexFile, string blobFile, int targetIndex)
        {
            byte[] idxBytes = File.ReadAllBytes(indexFile); int entryCount = idxBytes.Length / 56;
            var entries = new List<CPackedEntry>(entryCount);
            using (BinaryReader idxReader = new BinaryReader(new MemoryStream(idxBytes)))
            {
                for (int i = 0; i < entryCount; i++)
                {
                    CPackedEntry entry = new CPackedEntry();
                    entry.BlobOffset = idxReader.ReadUInt32();
                    entry.Width = idxReader.ReadUInt32();
                    entry.Height = idxReader.ReadUInt32();
                    entry.Flags = idxReader.ReadUInt32();
                    entry.AnchorX = idxReader.ReadUInt32();
                    entry.AnchorY = idxReader.ReadUInt32();
                    entry.WidthInner = idxReader.ReadUInt32();
                    entry.HeightInner = idxReader.ReadUInt32();
                    entry.PackedDims = idxReader.ReadUInt32();
                    entry.Reserved = new uint[5];
                    for (int j = 0; j < 5; j++) entry.Reserved[j] = idxReader.ReadUInt32();
                    entries.Add(entry);
                }
            }

            if (targetIndex < 0 || targetIndex >= entries.Count)
                throw new ArgumentOutOfRangeException(nameof(targetIndex), "Такого номера спрайта нет в индексе.");

            var entryTarget = entries[targetIndex];

            using (FileStream blobFs = File.OpenRead(blobFile))
            using (BinaryReader blobReader = new BinaryReader(blobFs))
            {
                // Обработаем как в оригинале: фильтр по Flags, но будем возвращать спрайт независимо от этого
                // Если нужно строго стандартные спрайты: можно проверить (entryTarget.Flags & 0x01) и т.д.

                if ((entryTarget.Flags & 0x01) != 0) // сжатый блок с заголовком
                {
                    blobFs.Position = entryTarget.BlobOffset;
                    uint uncompressedSize = blobReader.ReadUInt32();

                    long nextOffset = (targetIndex + 1 < entries.Count) ? entries[targetIndex + 1].BlobOffset : blobFs.Length;
                    int compressedSize = (int)(nextOffset - entryTarget.BlobOffset - 4);

                    byte[] gameLzoStream = blobReader.ReadBytes(compressedSize);

                    byte[] lzoInputForYourMethod = new byte[gameLzoStream.Length + 4];
                    Array.Copy(gameLzoStream, 0, lzoInputForYourMethod, 0, gameLzoStream.Length);
                    byte[] lenBytes = BitConverter.GetBytes(uncompressedSize);
                    Array.Copy(lenBytes, 0, lzoInputForYourMethod, gameLzoStream.Length, 4);

                    byte[] payload = compressor.Decompress(lzoInputForYourMethod);

                    uint totalSize = BitConverter.ToUInt32(payload, 0);
                    uint pixelDataOffset = BitConverter.ToUInt32(payload, 4);
                    ushort width = BitConverter.ToUInt16(payload, 8);
                    ushort height = BitConverter.ToUInt16(payload, 10);

                    var wb = new WriteableBitmap(width, height, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null);
                    byte[] pixels = new byte[width * height * 4];

                    int cursor = 12;
                    for (int y = 0; y < height; y++)
                    {
                        ushort numSpans = BitConverter.ToUInt16(payload, cursor);
                        if (numSpans == 0)
                        {
                            cursor += 8;
                            continue;
                        }

                        uint spanPixelDataOffset = BitConverter.ToUInt32(payload, cursor + 2);
                        int spansStart = cursor + 6;
                        int pxCursor = (int)(pixelDataOffset + spanPixelDataOffset);

                        for (int s = 0; s < numSpans; s++)
                        {
                            int spanOffset = spansStart + (s * 4);
                            ushort startX = BitConverter.ToUInt16(payload, spanOffset);
                            ushort spanLen = BitConverter.ToUInt16(payload, spanOffset + 2);

                            for (int x = 0; x < spanLen; x++)
                            {
                                ushort rgb565 = BitConverter.ToUInt16(payload, pxCursor);
                                pxCursor += 2;

                                int r = ((rgb565 >> 11) & 0x1F) * 255 / 31;
                                int g = ((rgb565 >> 5) & 0x3F) * 255 / 63;
                                int b = (rgb565 & 0x1F) * 255 / 31;

                                int idxPix = (y * width + (startX + x)) * 4;
                                pixels[idxPix + 0] = (byte)b;
                                pixels[idxPix + 1] = (byte)g;
                                pixels[idxPix + 2] = (byte)r;
                                pixels[idxPix + 3] = 255;
                            }
                        }
                        cursor += (numSpans + 2) * 4;
                    }

                    wb.WritePixels(new Int32Rect(0, 0, width, height), pixels, width * 4, 0);
                    return wb;
                }
                else // несжатый или другой режим; размер 64x64 по умолчанию
                {
                    blobFs.Position = entryTarget.BlobOffset;
                    uint uncompressedSize = blobReader.ReadUInt32();

                    long nextOffset = (targetIndex + 1 < entries.Count) ? entries[targetIndex + 1].BlobOffset : blobFs.Length;
                    int compressedSize = (int)(nextOffset - entryTarget.BlobOffset - 4);

                    byte[] gameLzoStream = blobReader.ReadBytes(compressedSize);

                    byte[] lzoInputForYourMethod = new byte[gameLzoStream.Length + 4];
                    Array.Copy(gameLzoStream, 0, lzoInputForYourMethod, 0, gameLzoStream.Length);
                    byte[] lenBytes = BitConverter.GetBytes(uncompressedSize);
                    Array.Copy(lenBytes, 0, lzoInputForYourMethod, gameLzoStream.Length, 4);

                    byte[] payload = compressor.Decompress(lzoInputForYourMethod);

                    int width = 64;
                    int height = 64;

                    var wb2 = new WriteableBitmap(width, height, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null);
                    byte[] pixels2 = new byte[width * height * 4];

                    int pxCursor = 0;
                    for (int y = 0; y < height; y++)
                    {
                        for (int x = 0; x < width; x++)
                        {
                            ushort rgb565 = BitConverter.ToUInt16(payload, pxCursor);
                            pxCursor += 2;

                            int r = ((rgb565 >> 11) & 0x1F) * 255 / 31;
                            int g = ((rgb565 >> 5) & 0x3F) * 255 / 63;
                            int b = (rgb565 & 0x1F) * 255 / 31;

                            int idxPix = (y * width + x) * 4;
                            pixels2[idxPix + 0] = (byte)b;
                            pixels2[idxPix + 1] = (byte)g;
                            pixels2[idxPix + 2] = (byte)r;
                            pixels2[idxPix + 3] = 255;
                        }
                    }

                    wb2.WritePixels(new Int32Rect(0, 0, width, height), pixels2, width * 4, 0);
                    return wb2;
                }
            }
        }
    }
}