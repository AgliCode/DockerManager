import { useEffect, useState } from 'react'
import '../App.css'
import { HubConnectionBuilder } from '@microsoft/signalr';

function Home() {
  const [containers, setContainers] = useState([]);
  const [isLoading, setIsLoading] = useState(true);
  const [selectedContainerIds, setSelectedContainerIds] = useState([]);

  const [logsText, setLogsText] = useState('');
  const [isLoadingLogs, setIsLoadingLogs] = useState(false);
  const [logsError, setLogsError] = useState('');

  useEffect(() => {
    const connection = new HubConnectionBuilder()
        .withUrl('http://localhost:5241/containerHub')
        .withAutomaticReconnect()
        .build();
    const handleContainersChanged = async () => {
        try {
            const response = await fetch(
                'http://localhost:5241/api/containers'
            );

            if (!response.ok) {
                throw new Error('Nie udało się pobrać kontenerów.');
            }

            const data = await response.json();
            setContainers(data);
        } catch (error) {
            console.error('Błąd odświeżania kontenerów:', error);
        }
    };
    connection.on('ContainersChanged', () => {
      handleContainersChanged();
    });
    connection.start()
        .then(() => {
            console.log('Połączono z SignalR');
        })
        .catch(error => {
            console.error('Błąd połączenia z SignalR:', error);
        });

    return () => {
        connection.stop();
    };
}, []);

  useEffect(() => {
    async function loadContainers() {
      try {
        const response = await fetch('http://localhost:5241/api/containers')

        if (!response.ok) {
          throw new Error('Nie udało się pobrać kontenerów')
        }

        const data = await response.json()
        setContainers(data)
      } catch (error) {
        console.error('Błąd pobierania kontenerów:', error)
      } finally {
        setIsLoading(false)
      }
    }

    loadContainers()
  }, [])

  // Obsługa zaznaczania kontenerów
    function toggleContainer(id, e) {
        if (e.target.checked) {
            setSelectedContainerIds((prev) => [...prev, id]);
        } else {
            setSelectedContainerIds((prev) =>
                prev.filter((selectedId) => selectedId !== id)
            );
        }
    }
  
    async function showLogs() {
      if (selectedContainerIds.length === 0) {
        return;
    }

    setIsLoadingLogs(true);
    setLogsText('');
    setLogsError('');

    try {
        const response = await fetch(
            'http://localhost:5241/api/containers/logs',
            {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify({
                    containerIds: selectedContainerIds
                })
            }
        );

        if (!response.ok) {
            throw new Error('Nie udało się pobrać logów.');
        }

        const data = await response.json();

        setLogsText(data.logs);
    } catch (error) {
        console.error('Błąd pobierania logów:', error);
        setLogsError(error.message);
    } finally {
        setIsLoadingLogs(false);
    }
    }

  return (
    <>
      <div className="command-center">
        <h3 className="section-title">Lista kontenerów</h3>
          {isLoading ? (
              <p className="status-message">
                  Ładowanie zasobów Dockera...
              </p>
          ) : containers.length === 0 ? (
              <p className="status-message">
                  Brak aktywnych kontenerów w systemie.
              </p>
          ) : (
              <div className="container-list">
                  {containers.map((c) => (
                      <div className="container-item" key={c.id}>
                        <input type="checkbox" 
                              className="styled-checkbox"
                              checked= {selectedContainerIds.includes(c.id)}
                              onChange={(e) => toggleContainer(c.id, e)} />
                        <div className="container-info">
                            <span className="info-badge">ID: {c.id.substring(0, 12)}</span>
                            <span className="info-name"> {c.names.join(", ").replaceAll("/", "")} </span>
                            <span className={`status-pill ${c.state.toLowerCase()}`}>{c.state}</span>
                        </div>
                      </div>
                  ))}
              </div>
          )}
        
        <h3 className="section-title">Wybrane kontenery</h3>
          {containers.length === 0 && isLoading ? (
            <p className="status-message">
              Ładowanie zasobów Dockera...
            </p>
          ) : selectedContainerIds.length === 0 ? (
            <p className="status-message">
              Nie wybrano żadnego kontenera operacyjnego.
            </p>
          ) : (
            <>
              <div className="selected-grid">
                {containers
                  .filter((c) => selectedContainerIds.includes(c.id))
                  .map((c) => (
                    <div className="selected-item" key={c.id}>
                      <strong>
                        {c.names.join(", ").replaceAll("/", "")}
                      </strong>

                      <small>ID: {c.id.substring(0, 12)}</small>
                    </div>
                  ))}
              </div>
              <p className="counter-text">
                Wybrano: <strong>{selectedContainerIds.length}</strong>
              </p>
            </>
          ) }


      <button
        type="button"
        className="btn-primary"
        onClick={showLogs}
        disabled={selectedContainerIds.length === 0}>
        Wyświetl logi
      </button>

      {isLoadingLogs ? (
      <p className="status-message">Pobieranie logów...</p>
        ) : logsError ? (
            <p className="status-message">{logsError}</p>
        ) : logsText ? (
            <div className="logs-container">
                <pre className="logs-output">{logsText}</pre>
            </div>
        ) : null}
  
      
      </div>
    
    
    </>
  )
}

export default Home
