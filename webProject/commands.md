# 🚀 Команди для запуску та тестування системи

## 📋 Швидкий старт

### Запустити все одразу:
```bash
cd "/Users/andriykahnovets/Desktop/MyFolder/University/III year/web/webProject"
./start-both-servers.sh
```

---

## 🛑 ЗУПИНКА

### Зупинити всі сервери ASP.NET:
```bash
pkill -9 -f "dotnet.*webProject"
```

### Зупинити Nginx:
```bash
sudo pkill -9 nginx
# АБО
brew services stop nginx
```

### Зупинити Redis:
```bash
brew services stop redis
# АБО
redis-cli shutdown
```

### Зупинити ВСЕ одразу:
```bash
pkill -9 -f "dotnet.*webProject"
sudo pkill -9 nginx
brew services stop redis
```

---

## 🧹 ОЧИЩЕННЯ

### Очистити Redis (видалити всі дані):
```bash
redis-cli FLUSHALL
```

### Очистити порти 80, 5001, 5002:
```bash
sudo lsof -ti:80,5001,5002 | xargs sudo kill -9 2>/dev/null
```

### Очистити логи Nginx:
```bash
sudo rm -f /opt/homebrew/var/log/nginx/*.log
sudo touch /opt/homebrew/var/log/nginx/error.log
sudo touch /opt/homebrew/var/log/nginx/access.log
```

### Очистити логи серверів:
```bash
rm -f server-5001.log server-5002.log
```

### Повне очищення (все разом):
```bash
pkill -9 -f "dotnet.*webProject"
sudo pkill -9 nginx
redis-cli FLUSHALL
sudo lsof -ti:80,5001,5002 | xargs sudo kill -9 2>/dev/null
rm -f server-5001.log server-5002.log
echo "✅ Все очищено!"
```

---

## ▶️ ЗАПУСК

### 1. Запустити Redis:
```bash
brew services start redis
# Перевірити:
redis-cli ping
```

### 2. Запустити Nginx:
```bash
# Перевірити конфігурацію:
sudo /opt/homebrew/opt/nginx/bin/nginx -t

# Запустити:
sudo /opt/homebrew/opt/nginx/bin/nginx
```

### 3. Запустити ASP.NET сервери:

**Варіант А - Обидва сервери одразу (у фоні):**
```bash
cd "/Users/andriykahnovets/Desktop/MyFolder/University/III year/web/webProject"
./start-both-servers.sh
```

**Варіант Б - Окремо в двох терміналах:**

*Термінал 1:*
```bash
cd "/Users/andriykahnovets/Desktop/MyFolder/University/III year/web/webProject"
./start-server-5001.sh
```

*Термінал 2:*
```bash
cd "/Users/andriykahnovets/Desktop/MyFolder/University/III year/web/webProject"
./start-server-5002.sh
```

**Варіант В - Вручну з параметрами:**
```bash
cd "/Users/andriykahnovets/Desktop/MyFolder/University/III year/web/webProject/webProject"
export ASPNETCORE_ENVIRONMENT=Production
dotnet run --urls "http://localhost:5001" --no-launch-profile -- --ServerInfo:ServerName=SERVER-5001 --ServerInfo:Port=5001
```

---

## 🔍 ПЕРЕВІРКА СТАТУСУ

### Перевірити чи працює Nginx:
```bash
ps aux | grep '[n]ginx'
sudo lsof -i :80
curl -I http://localhost
```

### Перевірити чи працюють ASP.NET сервери:
```bash
ps aux | grep '[d]otnet'
lsof -i :5001
lsof -i :5002
curl http://localhost:5001
curl http://localhost:5002
```

### Перевірити чи працює Redis:
```bash
redis-cli ping
brew services list | grep redis
```

### Перевірити всі порти:
```bash
sudo lsof -i :80,5001,5002
```

### Показати статус brew services:
```bash
brew services list
```

---

## 📊 МОНІТОРИНГ ЛОГІВ

### Логи Nginx:
```bash
# Access лог (запити):
tail -f /opt/homebrew/var/log/nginx/access.log

# Error лог (помилки):
tail -f /opt/homebrew/var/log/nginx/error.log
```

### Логи ASP.NET серверів:
```bash
# Сервер 5001:
tail -f server-5001.log

# Сервер 5002:
tail -f server-5002.log

# Обидва одразу:
tail -f server-5001.log server-5002.log
```

### Логи Redis:
```bash
redis-cli monitor
```

---

## 🧪 ТЕСТУВАННЯ БАЛАНСУВАННЯ

### Тест 1 - Перевірити балансування:
```bash
# Виконай кілька разів і подивись на різні Upstream адреси:
curl http://localhost
curl http://localhost
curl http://localhost
```

### Тест 2 - Моніторинг балансування в реальному часі:
```bash
# В одному терміналі:
tail -f /opt/homebrew/var/log/nginx/access.log

# В іншому терміналі виконуй запити:
for i in {1..10}; do curl -s http://localhost > /dev/null && echo "Request $i sent"; done
```

### Тест 3 - Перевірити SignalR:
```bash
# Відкрий браузер на http://localhost
# В консолі браузера не повинно бути помилок "SignalR not loaded"
```

### Тест 4 - Перевірити Redis черги:
```bash
# Подивись всі ключі в Redis:
redis-cli KEYS "*"

# Подивись довжину черги:
redis-cli LLEN matrix_task_queue

# Подивись задачі в черзі:
redis-cli LRANGE matrix_task_queue 0 -1
```

---

## 🔧 НАЛАГОДЖЕННЯ

### Якщо Nginx не запускається:
```bash
# Перевір конфігурацію:
sudo /opt/homebrew/opt/nginx/bin/nginx -t

# Подивись помилки:
tail -20 /opt/homebrew/var/log/nginx/error.log

# Перезапусти:
sudo pkill -9 nginx
sudo rm -f /opt/homebrew/var/run/nginx.pid
sudo /opt/homebrew/opt/nginx/bin/nginx
```

### Якщо сервери не запускаються:
```bash
# Перевір чи вільні порти:
lsof -i :5001
lsof -i :5002

# Очисти порти:
sudo lsof -ti:5001,5002 | xargs sudo kill -9

# Спробуй запустити вручну щоб побачити помилки:
cd "/Users/andriykahnovets/Desktop/MyFolder/University/III year/web/webProject/webProject"
dotnet run --urls "http://localhost:5001"
```

### Якщо SignalR не працює:
```bash
# Перевір чи є файл SignalR:
ls -la "/Users/andriykahnovets/Desktop/MyFolder/University/III year/web/webProject/webProject/wwwroot/lib/signalr/dist/browser/signalr.min.js"

# Якщо немає, встанови:
cd "/Users/andriykahnovets/Desktop/MyFolder/University/III year/web/webProject/webProject"
libman restore
```

### Якщо Redis не працює:
```bash
# Запусти Redis:
brew services start redis

# Перевір:
redis-cli ping

# Якщо не відповідає, подивись логи:
brew services info redis
```

---

## 🔄 ПОВНИЙ ЦИКЛ ПЕРЕЗАПУСКУ

### Скопіюй цей блок для повного перезапуску:
```bash
# 1. ЗУПИНКА
echo "🛑 Зупинка..."
pkill -9 -f "dotnet.*webProject"
sudo pkill -9 nginx
sleep 2

# 2. ОЧИЩЕННЯ
echo "🧹 Очищення..."
redis-cli FLUSHALL
sudo lsof -ti:80,5001,5002 | xargs sudo kill -9 2>/dev/null
sudo rm -f /opt/homebrew/var/run/nginx.pid
sleep 1

# 3. ЗАПУСК
echo "▶️ Запуск..."

# Redis
brew services start redis
sleep 1

# Nginx
sudo /opt/homebrew/opt/nginx/bin/nginx
sleep 1

# ASP.NET сервери
cd "/Users/andriykahnovets/Desktop/MyFolder/University/III year/web/webProject"
./start-both-servers.sh

# 4. ПЕРЕВІРКА
echo ""
echo "✅ Перевірка статусу..."
sleep 3
echo "Nginx: $(ps aux | grep -c '[n]ginx') процесів"
echo "Dotnet: $(ps aux | grep -c '[d]otnet') процесів"
echo "Redis: $(redis-cli ping)"
echo ""
echo "🌐 Сайт: http://localhost"
```

---

## 📝 КОРИСНІ ALIAS (додай в ~/.zshrc)

```bash
# Додай ці рядки в ~/.zshrc для швидкого доступу:

alias webproject='cd "/Users/andriykahnovets/Desktop/MyFolder/University/III year/web/webProject"'
alias webstart='webproject && ./start-both-servers.sh'
alias webstop='pkill -9 -f "dotnet.*webProject"'
alias webclean='pkill -9 -f "dotnet.*webProject"; redis-cli FLUSHALL; echo "✅ Очищено"'
alias weblogs='tail -f /opt/homebrew/var/log/nginx/access.log'
alias webstatus='echo "Nginx:"; ps aux | grep -c "[n]ginx"; echo "Dotnet:"; ps aux | grep -c "[d]otnet"; echo "Redis:"; redis-cli ping'
```

Після додавання виконай:
```bash
source ~/.zshrc
```

Тоді зможеш використовувати:
- `webstart` - запустити все
- `webstop` - зупинити сервери
- `webclean` - очистити все
- `weblogs` - подивитись логи
- `webstatus` - перевірити статус

---

## 🎯 ШВИДКА ШПАРГАЛКА

| Дія | Команда |
|-----|---------|
| Запустити все | `./start-both-servers.sh` |
| Зупинити сервери | `pkill -9 -f "dotnet.*webProject"` |
| Очистити Redis | `redis-cli FLUSHALL` |
| Перезапустити Nginx | `sudo pkill -9 nginx && sudo /opt/homebrew/opt/nginx/bin/nginx` |
| Логи Nginx | `tail -f /opt/homebrew/var/log/nginx/access.log` |
| Логи серверів | `tail -f server-5001.log server-5002.log` |
| Статус портів | `sudo lsof -i :80,5001,5002` |
| Тест балансування | `for i in {1..5}; do curl http://localhost; done` |

---

**Створено:** 10 грудня 2025  
**Версія:** 1.0

