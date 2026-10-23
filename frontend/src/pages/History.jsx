import { useEffect, useState } from 'react'
import '../App.css'

function History() {
  const [selectedEntry, setSelectedEntry] = useState(null);
  const [historyEntries, setHistoryEntries] = useState([]);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    async function loadHistory() {
      try {
        const response = await fetch('http://localhost:5241/api/history')

        if (!response.ok) {
          throw new Error('Nie udało się pobrać historii')
        }

        const data = await response.json()
        setHistoryEntries(data)
      } catch (error) {
        console.error('Błąd pobierania historii:', error)
      } finally {
        setIsLoading(false)
      }
    }

    loadHistory()
  }, [])

  const GetContainerNames = (containersJson) => {
    try {
      const containers = JSON.parse(containersJson);

      if (!Array.isArray(containers)) {
        return [];
      }

      const names = [];

      for (const container of containers) {
        if (
          container !== null &&
          typeof container === 'object' &&
          !Array.isArray(container) &&
          Object.hasOwn(container, 'Name')
        ) {
          names.push(container.Name ?? 'Nieznany kontener');
        } else if (typeof container === 'string') {
          names.push(container ?? 'Nieznany kontener');
        }
      }

      return names;
    } catch (error) {
      if (error instanceof SyntaxError) {
        return ['Nie udało się odczytać danych kontenerów'];
      }

      throw error;
    }
  }

  const ShowHistoryLogs = (entry) => {
    setSelectedEntry(entry)
  }

  const CloseHistoryLogs = () => {
    setSelectedEntry(null);
  }

  return (
    <div className="command-center">
      <h3 className="section-title">Historia sprawdzania logów</h3>
        { isLoading ? (
          <p className="status-message">
              Ładowanie historii...
          </p>
        ) : historyEntries.length == 0 ? (
          <p className="status-message">
              Nie ma jeszcze zapisanych sprawdzeń logów.
          </p>
        ) : (
          <div className="history-table-wrapper">
            <table className="history-table">
                <thead>
                    <tr>
                        <th>Data i godzina</th>
                        <th>Sprawdzone kontenery</th>
                        <th>Filtry</th>
                        <th>Akcja</th>
                    </tr>
                </thead>
                <tbody>
                    { historyEntries.map((entry) => (
                      <tr key={entry.id}>
                          <td>{new Date(entry.checkedAt).toLocaleString('pl-PL')}</td>
                          <td>
                              {GetContainerNames(entry.containersJson).map((container, index)=>( 
                                <div key={index} className="history-container-name">{container}</div>
                              ))}
                          </td>
                          <td>
                          </td>
                          <td>
                              <button type="button" className="btn btn-primary" onClick={() => ShowHistoryLogs(entry)}>
                                  Podgląd logów
                              </button>
                          </td>
                      </tr>
        
                    ))}
                </tbody>
            </table> 
          </div>
        )}
        
        {selectedEntry != null && (
  <section className="history-preview">
    <div className="history-preview-header">
      <h4 className="history-preview-title">
        Podgląd zapisanych logów
      </h4>

      <button
        type="button"
        className="btn btn-primary"
        onClick={CloseHistoryLogs}
      >
        Zamknij podgląd
      </button>
    </div>

    <pre className="history-logs">
      {selectedEntry.logsText ?? 'Brak zapisanych logów'}
    </pre>
  </section>
)}
    </div>
  )
}
export default History




