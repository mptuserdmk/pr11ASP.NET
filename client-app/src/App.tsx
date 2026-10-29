import React, { useState, useEffect } from 'react';

interface JwtPayload {
  sub?: string;
  name?: string;
  role?: string;
  isActive?: string;
  exp?: number;
}

interface LogEntry {
  id: string;
  time: string;
  method: string;
  url: string;
  status: number | string;
  headers: Record<string, string>;
  data: any;
}

export default function App() {
  const [apiUrl, setApiUrl] = useState('http://localhost:5019');
  const [login, setLogin] = useState('user');
  const [password, setPassword] = useState('user123');
  const [token, setToken] = useState<string>(() => localStorage.getItem('jwt_token') || '');
  const [userProfile, setUserProfile] = useState<JwtPayload | null>(null);
  const [logs, setLogs] = useState<LogEntry[]>([]);
  const [isLoading, setIsLoading] = useState(false);

  // Декодирование токена
  useEffect(() => {
    if (!token) {
      setUserProfile(null);
      return;
    }
    try {
      const parts = token.split('.');
      if (parts.length === 3) {
        const decoded = JSON.parse(atob(parts[1]));
        setUserProfile({
          sub: decoded.sub || decoded['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier'],
          name: decoded.name || decoded['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name'],
          role: decoded.role || decoded['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'],
          isActive: decoded.isActive,
          exp: decoded.exp
        });
      } else {
        setUserProfile(null);
      }
    } catch {
      setUserProfile(null);
    }
  }, [token]);

  const addLog = (method: string, url: string, status: number | string, headers: Record<string, string>, data: any) => {
    const entry: LogEntry = {
      id: Math.random().toString(36).substring(2, 9),
      time: new Date().toLocaleTimeString(),
      method,
      url,
      status,
      headers,
      data
    };
    setLogs(prev => [entry, ...prev]);
  };

  const handleLogin = async (e?: React.FormEvent) => {
    if (e) e.preventDefault();
    setIsLoading(true);
    try {
      const res = await fetch(`${apiUrl}/api/auth/login`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ login, password })
      });

      const data = await res.json().catch(() => ({}));
      const headersObj: Record<string, string> = {};
      res.headers.forEach((v, k) => { headersObj[k] = v; });

      addLog('POST', `${apiUrl}/api/auth/login`, res.status, headersObj, data);

      if (res.ok && data.token) {
        setToken(data.token);
        localStorage.setItem('jwt_token', data.token);
      }
    } catch (err: any) {
      addLog('POST', `${apiUrl}/api/auth/login`, 'ERR', {}, { error: err.message || 'Сетевая ошибка / CORS' });
    } finally {
      setIsLoading(false);
    }
  };

  const handleRegister = async () => {
    setIsLoading(true);
    try {
      const res = await fetch(`${apiUrl}/api/auth/register`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ login, password })
      });

      const data = await res.json().catch(() => ({}));
      const headersObj: Record<string, string> = {};
      res.headers.forEach((v, k) => { headersObj[k] = v; });

      addLog('POST', `${apiUrl}/api/auth/register`, res.status, headersObj, data);

      if (res.ok && data.token) {
        setToken(data.token);
        localStorage.setItem('jwt_token', data.token);
      }
    } catch (err: any) {
      addLog('POST', `${apiUrl}/api/auth/register`, 'ERR', {}, { error: err.message });
    } finally {
      setIsLoading(false);
    }
  };

  const handleLogout = () => {
    setToken('');
    localStorage.removeItem('jwt_token');
    addLog('AUTH', 'Logout', 'INFO', {}, { message: 'Токен очищен из памяти и localStorage' });
  };

  const handleCorruptToken = () => {
    const broken = 'invalid_corrupted_token_12345';
    setToken(broken);
    localStorage.setItem('jwt_token', broken);
    addLog('AUTH', 'Corrupt Token', 'INFO', {}, { message: 'Установлен поврежденный токен для проверки 401' });
  };

  // ТЕСТ 1: 200 OK — Свой ресурс (GET /api/auth/me)
  const testMyResource = async () => {
    try {
      const res = await fetch(`${apiUrl}/api/auth/me`, {
        headers: { Authorization: `Bearer ${token}` }
      });
      const data = await res.json().catch(() => ({}));
      addLog('GET', `${apiUrl}/api/auth/me`, res.status, {}, data);
    } catch (err: any) {
      addLog('GET', `${apiUrl}/api/auth/me`, 'ERR', {}, { error: err.message });
    }
  };

  // ТЕСТ 2: 403 Forbidden — Resource-based policy (Запрос чужого заказа)
  const testResourcePolicy = async () => {
    try {
      // Запрашиваем заказ ID 9999 или 1
      const res = await fetch(`${apiUrl}/api/orders/9999`, {
        headers: { Authorization: `Bearer ${token}` }
      });
      const data = await res.json().catch(() => ({}));
      addLog('GET', `${apiUrl}/api/orders/9999`, res.status, {}, data);
    } catch (err: any) {
      addLog('GET', `${apiUrl}/api/orders/9999`, 'ERR', {}, { error: err.message });
    }
  };

  // ТЕСТ 3: Role-based policy (GET /api/users) — доступен только Admin
  const testRolePolicy = async () => {
    try {
      const res = await fetch(`${apiUrl}/api/users`, {
        headers: { Authorization: `Bearer ${token}` }
      });
      const data = await res.json().catch(() => ({}));
      addLog('GET', `${apiUrl}/api/users`, res.status, {}, data);
    } catch (err: any) {
      addLog('GET', `${apiUrl}/api/users`, 'ERR', {}, { error: err.message });
    }
  };

  // ТЕСТ 4: 401 Unauthorized — Запрос без токена
  const testUnauthorized = async () => {
    try {
      const res = await fetch(`${apiUrl}/api/auth/me`);
      const data = await res.json().catch(() => ({}));
      addLog('GET', `${apiUrl}/api/auth/me [No Token]`, res.status, {}, data);
    } catch (err: any) {
      addLog('GET', `${apiUrl}/api/auth/me [No Token]`, 'ERR', {}, { error: err.message });
    }
  };

  // ТЕСТ 5: 429 Too Many Requests — Спам запросами на вход (Rate Limiting)
  const testRateLimiting = async () => {
    addLog('TEST', 'Rate Limiting Burst', 'INFO', {}, { message: 'Отправка серии из 6 запросов подряд на /api/auth/login...' });
    for (let i = 1; i <= 6; i++) {
      try {
        const res = await fetch(`${apiUrl}/api/auth/login`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ login: 'burst_test', password: 'password123' })
        });
        const data = await res.json().catch(() => ({}));
        addLog('POST', `[Попытка ${i}/6] /api/auth/login`, res.status, {}, data);
      } catch (err: any) {
        addLog('POST', `[Попытка ${i}/6] /api/auth/login`, 'ERR', {}, { error: err.message });
      }
    }
  };

  // ТЕСТ 6: CORS и чтение кастомного заголовка X-Total-Count
  const testCorsExposedHeader = async () => {
    try {
      const res = await fetch(`${apiUrl}/api/products?page=1&pageSize=3`);
      const data = await res.json().catch(() => ({}));
      const totalCountHeader = res.headers.get('X-Total-Count');

      addLog('GET', `${apiUrl}/api/products (CORS Check)`, res.status, {
        'Access-Control-Allow-Origin': res.headers.get('access-control-allow-origin') || 'Доступен браузеру',
        'X-Total-Count': totalCountHeader || 'null (Заголовок не экспонирован)'
      }, {
        success: res.ok,
        xTotalCountHeader: totalCountHeader,
        itemsReceived: Array.isArray(data) ? data.length : 0,
        explanation: totalCountHeader 
          ? `УСПЕХ CORS: Заголовок X-Total-Count = "${totalCountHeader}" успешно прочитан клиентским JS благодаря WithExposedHeaders("X-Total-Count")!` 
          : 'ОШИБКА: Заголовок X-Total-Count недоступен JS'
      });
    } catch (err: any) {
      addLog('GET', `${apiUrl}/api/products (CORS Check)`, 'ERR', {}, { error: err.message });
    }
  };

  return (
    <div className="container">
      <header>
        <div>
          <h1>Клиент ASP.NET Core: JWT, Policies, CORS & Rate Limiting</h1>
          <p style={{ color: 'var(--text-muted)', fontSize: '0.85rem' }}>
            Фронтенд Origin: <strong>http://localhost:5173</strong>
          </p>
        </div>
        <div>
          {userProfile ? (
            <span className={`badge badge-${userProfile.role?.toLowerCase() === 'admin' ? 'admin' : 'user'}`}>
              {userProfile.role}: {userProfile.name} (ID: {userProfile.sub})
            </span>
          ) : (
            <span className="badge badge-guest">Не аутентифицирован</span>
          )}
        </div>
      </header>

      {/* Конфигурация API URL */}
      <div style={{ marginBottom: '20px', display: 'flex', gap: '10px', alignItems: 'center' }}>
        <label style={{ margin: 0, whiteSpace: 'nowrap' }}>Адрес API:</label>
        <input 
          type="text" 
          value={apiUrl} 
          onChange={e => setApiUrl(e.target.value)} 
          style={{ maxWidth: '300px' }} 
        />
      </div>

      <div className="grid">
        {/* Левая колонка: Аутентификация и Тесты */}
        <div style={{ display: 'flex', flexDirection: 'column', gap: '20px' }}>
          
          {/* Карточка авторизации */}
          <div className="card">
            <h2>Вход и регистрация</h2>
            <form onSubmit={handleLogin}>
              <div className="form-group">
                <label>Логин:</label>
                <input 
                  type="text" 
                  value={login} 
                  onChange={e => setLogin(e.target.value)} 
                  required 
                />
              </div>
              <div className="form-group">
                <label>Пароль:</label>
                <input 
                  type="password" 
                  value={password} 
                  onChange={e => setPassword(e.target.value)} 
                  required 
                />
              </div>

              {/* Быстрые пресеты */}
              <div style={{ display: 'flex', gap: '8px', marginBottom: '14px' }}>
                <button 
                  type="button" 
                  className="btn-chip"
                  onClick={() => { setLogin('admin'); setPassword('admin123'); }}
                >
                  Заполнить: admin (Admin)
                </button>
                <button 
                  type="button" 
                  className="btn-chip"
                  onClick={() => { setLogin('user'); setPassword('user123'); }}
                >
                  Заполнить: user (User)
                </button>
              </div>

              <div className="btn-group">
                <button type="submit" className="btn-primary" disabled={isLoading}>
                  Войти (POST /api/auth/login)
                </button>
                <button type="button" className="btn-secondary" onClick={handleRegister} disabled={isLoading}>
                  Зарегистрироваться
                </button>
                {token && (
                  <button type="button" className="btn-danger" onClick={handleLogout}>
                    Выйти
                  </button>
                )}
              </div>
            </form>

            {/* Блок текущего токена */}
            {token && (
              <div style={{ marginTop: '16px' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                  <label style={{ margin: 0 }}>Текущий JWT Access-токен:</label>
                  <button type="button" className="btn-chip" onClick={handleCorruptToken} title="Устанавливает битую строку для проверки 401">
                    Испортить токен
                  </button>
                </div>
                <div className="token-box">{token}</div>
              </div>
            )}
          </div>

          {/* Карточка тестового пульта */}
          <div className="card">
            <h2>Интерактивный тестовый пульт</h2>
            <div className="test-grid">
              
              {/* Тест 1 */}
              <div className="test-item">
                <div className="test-info">
                  <div className="test-title">Тест 1: Запрос своего ресурса (200 OK)</div>
                  <div className="test-desc">GET /api/auth/me с текущим Bearer токеном</div>
                </div>
                <button className="btn-secondary" onClick={testMyResource}>Проверить</button>
              </div>

              {/* Тест 2 */}
              <div className="test-item">
                <div className="test-info">
                  <div className="test-title">Тест 2: Resource-Based Policy (403 / 200)</div>
                  <div className="test-desc">GET /api/orders/9999 (проверка владения ресурсом)</div>
                </div>
                <button className="btn-secondary" onClick={testResourcePolicy}>Проверить</button>
              </div>

              {/* Тест 3 */}
              <div className="test-item">
                <div className="test-info">
                  <div className="test-title">Тест 3: Role-Based Policy (Admin only)</div>
                  <div className="test-desc">GET /api/users (403 для user, 200 для admin)</div>
                </div>
                <button className="btn-secondary" onClick={testRolePolicy}>Проверить</button>
              </div>

              {/* Тест 4 */}
              <div className="test-item">
                <div className="test-info">
                  <div className="test-title">Тест 4: Запрос без токена (401 Unauthorized)</div>
                  <div className="test-desc">GET /api/auth/me без заголовка Authorization</div>
                </div>
                <button className="btn-secondary" onClick={testUnauthorized}>Проверить</button>
              </div>

              {/* Тест 5 */}
              <div className="test-item">
                <div className="test-info">
                  <div className="test-title">Тест 5: Rate Limiting (429 Too Many Requests)</div>
                  <div className="test-desc">Серия из 6 запросов подряд на /api/auth/login</div>
                </div>
                <button className="btn-secondary" onClick={testRateLimiting}>Проверить</button>
              </div>

              {/* Тест 6 */}
              <div className="test-item">
                <div className="test-info">
                  <div className="test-title">Тест 6: CORS + Exposed Header X-Total-Count</div>
                  <div className="test-desc">GET /api/products с чтением заголовка X-Total-Count</div>
                </div>
                <button className="btn-secondary" onClick={testCorsExposedHeader}>Проверить</button>
              </div>

            </div>
          </div>

        </div>

        {/* Правая колонка: Журнал сетевых ответов */}
        <div className="card" style={{ display: 'flex', flexDirection: 'column' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '14px' }}>
            <h2 style={{ margin: 0 }}>Журнал ответов API</h2>
            {logs.length > 0 && (
              <button className="btn-chip" onClick={() => setLogs([])}>Очистить</button>
            )}
          </div>

          <div className="log-box" style={{ flex: 1 }}>
            {logs.length === 0 ? (
              <div style={{ color: 'var(--text-muted)', textAlign: 'center', padding: '40px 0' }}>
                Нажмите любую кнопку теста для отправки запроса к API
              </div>
            ) : (
              logs.map(log => {
                let statusClass = 'status-200';
                if (log.status === 401) statusClass = 'status-401';
                else if (log.status === 403) statusClass = 'status-403';
                else if (log.status === 429) statusClass = 'status-429';
                else if (typeof log.status === 'number' && log.status >= 400) statusClass = 'status-403';

                return (
                  <div key={log.id} className="log-entry">
                    <div className="log-header">
                      <span>[{log.time}] {log.method} {log.url}</span>
                      <span className={statusClass}>HTTP {log.status}</span>
                    </div>
                    {Object.keys(log.headers).length > 0 && (
                      <div style={{ color: 'var(--accent)', fontSize: '0.725rem', marginBottom: '4px' }}>
                        Headers: {JSON.stringify(log.headers)}
                      </div>
                    )}
                    <div className="log-body">
                      {typeof log.data === 'string' ? log.data : JSON.stringify(log.data, null, 2)}
                    </div>
                  </div>
                );
              })
            )}
          </div>
        </div>

      </div>
    </div>
  );
}

