import re
import matplotlib.pyplot as plt
from datetime import datetime

def parse_data(log_data):
    """Парсит строки формата 'Time: 2024-12-14 16:30:16.57964; Byte's: 15657; Ok.'"""
    pattern = r"Time: (?P<time>\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d+); Byte's: (?P<bytes>\d+);"
    parsed_data = []

    for line in log_data.splitlines():
        match = re.match(pattern, line)
        if match:
            time_str = match.group('time')
            bytes_count = int(match.group('bytes'))

            # Преобразование строки времени в объект datetime
            time_obj = datetime.strptime(time_str, '%Y-%m-%d %H:%M:%S.%f')

            parsed_data.append((time_obj, bytes_count))

    return parsed_data

def calculate_throughput(parsed_data):
    """Вычисляет пропускную способность в байтах в секунду."""
    if len(parsed_data) < 2:
        print("Недостаточно данных для вычисления пропускной способности.")
        return 0

    # Общее количество данных
    total_bytes = sum(bytes_count for _, bytes_count in parsed_data)

    # Время между первым и последним событием
    start_time = parsed_data[0][0]
    end_time = parsed_data[-1][0]
    duration = (end_time - start_time).total_seconds()

    # Пропускная способность (байты в секунду)
    throughput = total_bytes / duration if duration > 0 else 0

    print(f"Общее количество данных: {total_bytes} байт")
    print(f"Общее время: {duration:.2f} секунд")
    print(f"Пропускная способность: {throughput:.2f} байт/секунда")

    return throughput

def plot_data(parsed_data):
    """Строит график отношения времени к количеству данных."""
    if not parsed_data:
        print("Нет данных для отображения.")
        return

    times, bytes_values = zip(*parsed_data)

    # Построение графика
    plt.figure(figsize=(12, 8))  # Увеличение размера графика
    plt.plot(times, bytes_values, marker='o', linestyle='-', color='b')
    plt.title('График отношения времени к количеству данных', fontsize=16)
    plt.xlabel('Время', fontsize=14)
    plt.ylabel('Количество данных (байты)', fontsize=14)
    plt.grid(True)
    plt.xticks(rotation=45)

    # Ограничение диапазона по оси Y (пример)
    plt.ylim(min(bytes_values) - 10, max(bytes_values) * 1.1)

    plt.tight_layout()
    plt.show()

# Загрузка данных из файла
file_path = 'LogFile.txt'  # Укажите путь к вашему файлу
try:
    with open(file_path, 'r', encoding='utf-8') as file:
        log_data = file.read()
except FileNotFoundError:
    print(f"Файл {file_path} не найден.")
    log_data = ""

# Парсинг данных
parsed_data = parse_data(log_data)

# Вычисление пропускной способности
throughput = calculate_throughput(parsed_data)

# Построение графика
plot_data(parsed_data)