/**
 * Inventory Movements Real-time Functionality
 * Handles SignalR connections and real-time updates for inventory movement tracking
 */

// Initialize when document is ready
document.addEventListener("DOMContentLoaded", function () {
    // Check if we're on the movements page
    if (!document.getElementById('movements-tbody')) {
        console.log("Not on inventory movements page, skipping initialization");
        return;
    }
    const searchInput = document.getElementById('tableSearch');
    const clearButton = document.getElementById('clearSearch');
    const movementRows = document.querySelectorAll('.movement-row');
    const tbody = document.getElementById('movements-tbody');
    
    // Get user and business IDs from the page
    const userId = document.body.getAttribute('data-user-id');
    const businessId = document.body.getAttribute('data-business-id');
    
    // Get current page and pagination info
    const currentPage = parseInt(document.body.getAttribute('data-current-page') || '1');
    const pageSize = parseInt(document.body.getAttribute('data-page-size') || '50');
    const totalItems = parseInt(document.body.getAttribute('data-total-items') || '0');
    const totalPages = parseInt(document.body.getAttribute('data-total-pages') || '1');
    
    // Track real-time additions for pagination
    let realTimeAdditions = parseInt(document.getElementById('realTimeAdditionsField')?.value || '0');
    console.log(`Current page: ${currentPage}, Real-time additions: ${realTimeAdditions}`);
    
    // Track processed batch IDs to avoid duplicate processing
    const processedBatchIds = new Set();
    
    // Track the last processed transaction ID
    let lastProcessedTransactionId = null;
    
    // Track processed movement IDs
    const processedMovementIds = new Set();
    
    // Initialize table search functionality
    initTableSearch();
    
    // Initialize SignalR connection
    initSignalRConnection();
    
    /**
     * Initialize table search functionality
     */
    function initTableSearch() {
        if (searchInput && clearButton) {
            // Handle search input
            searchInput.addEventListener('input', function() {
                const searchTerm = this.value.toLowerCase();
                filterTable(searchTerm);
            });
            
            // Handle clear button
            clearButton.addEventListener('click', function() {
                searchInput.value = '';
                filterTable('');
            });
        }
    }
    
    /**
     * Filter table based on search term
     * @param {string} searchTerm - Term to search for
     */
    function filterTable(searchTerm) {
        const rows = document.querySelectorAll('.movement-row');
        
        rows.forEach(row => {
            const text = row.textContent.toLowerCase();
            if (text.includes(searchTerm)) {
                row.style.display = '';
            } else {
                row.style.display = 'none';
            }
        });
        
        // Update summary numbers based on filtered rows
        updateSummaryNumbers();
    }
    
    /**
     * Initialize SignalR connection
     */
    function initSignalRConnection() {
        const connection = new signalR.HubConnectionBuilder()
            .withUrl("/realtimehub")
            .withAutomaticReconnect([0, 2000, 5000, 10000, 30000]) // Reconnect with exponential backoff
            .configureLogging(signalR.LogLevel.Information)
            .build();
            
        const connectionStatus = document.getElementById('connection-status');
        const realtimeSpinner = document.getElementById('realtime-spinner');
        
        // Connection status handlers
        connection.onreconnecting(() => {
            connectionStatus.textContent = "Reconnecting...";
            realtimeSpinner.classList.remove('d-none');
        });
        
        connection.onreconnected(() => {
            connectionStatus.textContent = "Connected - receiving real-time updates";
            realtimeSpinner.classList.add('d-none');
            
            // Re-join the business group
            connection.invoke("JoinInventoryGroup", businessId);
        });
        
        connection.onclose(() => {
            connectionStatus.textContent = "Disconnected";
            realtimeSpinner.classList.add('d-none');
            
            // Add a reconnect button
            if (!document.getElementById('reconnect-btn')) {
                const reconnectBtn = document.createElement('button');
                reconnectBtn.id = 'reconnect-btn';
                reconnectBtn.className = 'btn btn-sm btn-primary ms-2';
                reconnectBtn.textContent = 'Reconnect';
                reconnectBtn.addEventListener('click', startConnection);
                connectionStatus.parentNode.appendChild(reconnectBtn);
            }
        });
        
        // Handle receiving a new inventory movement
        connection.on("ReceiveInventoryMovement", (movement) => {
            console.log("Received inventory movement:", movement);
            
            // Add debugging information
            if (!movement) {
                console.error("Received null or undefined movement");
                return;
            }
            
            // Check if we have the special JSON.NET format
            if (movement.$id && !movement.id && !movement.Id) {
                console.warn("Received movement with JSON.NET format but no ID property");
            }

            const realTimeAdditionsField = document.getElementById('realTimeAdditionsField');
            if (realTimeAdditionsField) {
                realTimeAdditionsField.value += 1;
            }
            
            // Add the movement to the table
            addMovementRow(movement);
        });
          
        // Handle receiving batch inventory movements (for QuickSale transactions)
        connection.on("ReceiveBatchInventoryMovements", (movements) => {
            console.log("Received batch movements:", movements);

            try {
                // Handle both array and JSON.NET format
                let movementsArray = [];

                if (movements && movements.$values) {
                    movementsArray = movements.$values;
                } else if (Array.isArray(movements)) {
                    movementsArray = movements;
                } else if (movements) {
                    // Handle case where it's a single movement in batch format
                    movementsArray = [movements];
                }

                if (!movementsArray || movementsArray.length === 0) {
                    console.log("No movements in batch");
                    return;
                }

                console.log(`Processing ${movementsArray.length} movements`);

                // Update hidden field for form submissions
                const realTimeAdditionsField = document.getElementById('realTimeAdditionsField');
                if (realTimeAdditionsField) {
                    realTimeAdditionsField.value += movementsArray.length;
                }

                // Process each movement with error handling
                movementsArray.forEach((movement, index) => {
                    try {
                        if (!movement) {
                            console.error(`Null movement at index ${index}`);
                            return;
                        }

                        console.log(`Processing movement ${index + 1}/${movementsArray.length}`);
                        addMovementRow(movement, true); // Skip duplicate check for batches
                    } catch (error) {
                        console.error(`Error processing movement ${index + 1}:`, error);
                    }
                });
            } catch (error) {
                console.error("Error processing batch movements:", error);
            }
        });
        
        // Function to start the connection
        function startConnection() {
            // Remove any existing reconnect button
            const reconnectBtn = document.getElementById('reconnect-btn');
            if (reconnectBtn) {
                reconnectBtn.remove();
            }
            
            // Update status
            connectionStatus.textContent = "Connecting...";
            realtimeSpinner.classList.remove('d-none');
            
            // Start the connection
            connection.start()
                .then(() => {
                    console.log("Connected to SignalR hub");
                    connectionStatus.textContent = "Connected - receiving real-time updates";
                    realtimeSpinner.classList.add('d-none');
                    
                    // Join the business group to receive inventory updates
                    return connection.invoke("JoinInventoryGroup", businessId);
                })
                .then(() => {
                    console.log(`Joined inventory group for business ${businessId}`);
                })
                .catch(err => {
                    console.error("Error connecting to SignalR hub:", err);
                    connectionStatus.textContent = "Connection failed";
                    realtimeSpinner.classList.add('d-none');
                    
                    // Add reconnect button
                    if (!document.getElementById('reconnect-btn')) {
                        const reconnectBtn = document.createElement('button');
                        reconnectBtn.id = 'reconnect-btn';
                        reconnectBtn.className = 'btn btn-sm btn-primary ms-2';
                        reconnectBtn.textContent = 'Reconnect';
                        reconnectBtn.addEventListener('click', startConnection);
                        connectionStatus.parentNode.appendChild(reconnectBtn);
                    }
                });
        }
        
        // Start the connection
        startConnection();
    }
    
    /**
     * Add a new movement row to the table
     * @param {Object} movement - The movement data
     * @param {boolean} skipDuplicateCheck - If true, skip the duplicate check (for batch processing)
     */
    function addMovementRow(movement, skipDuplicateCheck = false) {
        console.group('addMovementRow');
        console.log("Starting addMovementRow with:", movement);

        try {
            // Check if we're on page 1
            const currentPage = parseInt(document.body.getAttribute('data-current-page') || '1');
            console.log(`Current page: ${currentPage}`);

            if (currentPage !== 1) {
                console.log(`Not on page 1 (current page: ${currentPage}), skipping display`);
                console.groupEnd();
                return;
            }

            console.log("addMovementRow received:", movement);
        
            if (!movement) {
                console.error("Received null or undefined movement in addMovementRow");
                return;
            }

            // Safely extract properties with fallbacks
            const id = movement?.id ?? movement?.Id;
            const productName = movement?.productName ?? movement?.ProductName ?? 'Unknown';
            const movementType = movement?.movementType ?? movement?.MovementType ?? 'Unknown';
            const quantityBefore = parseInt(movement?.quantityBefore ?? movement?.QuantityBefore ?? 0);
            const quantityAfter = parseInt(movement?.quantityAfter ?? movement?.QuantityAfter ?? 0);
            const referenceId = movement?.referenceId ?? movement?.ReferenceId ?? '';
            const notes = movement?.notes ?? movement?.Notes ?? '';
            const timestamp = new Date(movement.timestamp || movement.Timestamp);
        
            // Debug extracted properties
            console.log(`Extracted properties: id=${id}, product=${productName}, type=${movementType}, ref=${referenceId}`);
        
            // Debug the movement data
            console.log(`Processing movement: ID=${id}, Product=${productName}, Type=${movementType}, Change=${quantityBefore}->${quantityAfter}`);
        
            // Validate that we have an ID
            if (!id) {
                console.error("Movement is missing ID:", movement);
                return;
            }
        
            // Check if we've already processed this movement (unless skipDuplicateCheck is true)
            if (!skipDuplicateCheck && processedMovementIds.has(id.toString())) {
                console.log(`Movement ID ${id} already processed, skipping`);
                return;
            }
        
            // Check if the movement already exists in the table
            if (document.querySelector(`.movement-row[data-id="${id}"]`)) {
                console.log(`Movement ${id} already exists in table, skipping`);
                return; // Skip if already in the table
            }
        
            // Mark this movement as processed
            processedMovementIds.add(id.toString());
        
            // Increment real-time additions counter
            realTimeAdditions++;
            console.log(`Added movement ${id} (${productName}). Total additions: ${realTimeAdditions}`);

            console.log("checker 1");
        
            // Only add to the table if we're on page 1
            if (currentPage === 1) {
                // Format the timestamp
                const formattedDate = timestamp.toLocaleString();
            
                // Calculate the change
                const change = quantityAfter - quantityBefore;
                const changeClass = change >= 0 ? 'text-success' : 'text-danger';
                const changePrefix = change >= 0 ? '+' : '';
            
                // Create a new row element
                const row = document.createElement('tr');
                row.className = 'movement-row animate__animated animate__fadeInDown';
                row.setAttribute('data-id', id);
            
                // Extract transaction ID from referenceId if available (format: SALE-123-xyz)
                const transactionMatch = referenceId.match(/SALE-(\d+)(?:-([a-zA-Z0-9]+))?/);
                if (transactionMatch) {
                    const saleId = transactionMatch[1];
                    const transactionId = transactionMatch[2] || "";
                    row.setAttribute('data-sale-id', saleId);
                    if (transactionId) {
                        row.setAttribute('data-transaction-id', transactionId);
                    }
                }
            
                // Generate the badge HTML
                let badgeClass;
                switch(movementType) {
                    case 'Purchase': badgeClass = 'bg-primary'; break;
                    case 'Sale': badgeClass = 'bg-danger'; break;
                    case 'Stock In': badgeClass = 'bg-success'; break;
                    case 'Adjustment': badgeClass = 'bg-warning text-dark'; break;
                    case 'New Product': badgeClass = 'bg-info'; break;
                    case 'Edit Product': badgeClass = 'bg-secondary'; break;
                    case 'Delete Product': badgeClass = 'bg-dark'; break;
                    default: badgeClass = 'bg-secondary';
                }
            
                // Fill in the row content
                row.innerHTML = `
                    <td>${formattedDate}</td>
                    <td>${productName}</td>
                    <td><span class="badge ${badgeClass}">${movementType}</span></td>
                    <td class="text-end">${quantityBefore.toLocaleString()}</td>
                    <td class="text-end ${changeClass} change-value">
                        ${changePrefix}${change.toLocaleString()}
                    </td>
                    <td class="text-end">${quantityAfter.toLocaleString()}</td>
                    <td>
                        <span class="badge bg-light text-dark">${referenceId}</span>
                    </td>
                    <td>
                        <small class="text-muted">${notes}</small>
                    </td>
                `;
            
                // Add to the top of the table
                if (tbody.firstChild) {
                    tbody.insertBefore(row, tbody.firstChild);
                } else {
                    tbody.appendChild(row);
                }
            
                // Highlight the row briefly
                setTimeout(() => {
                    row.classList.add('table-info');
                    setTimeout(() => {
                        row.classList.remove('table-info');
                        row.classList.remove('animate__fadeInDown');
                    }, 3000);
                }, 100);
            
                // Update summary numbers
                updateSummaryNumbers();
            
                // Check if this movement is part of a multi-product transaction (QuickSale)
                // and highlight all rows from the same transaction
                if (referenceId.includes('SALE-')) {
                    const transactionMatch = referenceId.match(/SALE-(\d+)(?:-([a-zA-Z0-9]+))?/);
                    if (transactionMatch) {
                        const saleId = transactionMatch[1];
                        const transactionId = transactionMatch[2] || "";
                    
                        let selector = `[data-sale-id="${saleId}"]`;
                        if (transactionId) {
                            selector = `[data-transaction-id="${transactionId}"]`;
                        }
                    
                        const relatedRows = document.querySelectorAll(selector);
                        if (relatedRows.length > 1) {
                            // Multiple rows from same transaction - highlight them together
                            relatedRows.forEach(relatedRow => {
                                relatedRow.classList.add('transaction-group');
                            });
                        
                            // Add visual indicator for grouped transaction
                            const indicator = document.createElement('div');
                            indicator.className = 'transaction-indicator';
                            indicator.innerHTML = `<small class="badge bg-info ms-2">Multi-product transaction (${relatedRows.length} items)</small>`;
                        
                            // Only add the indicator once to the first row in the group
                            const firstRow = relatedRows[0];
                            if (firstRow && !firstRow.querySelector('.transaction-indicator')) {
                                const firstCell = firstRow.querySelector('td:first-child');
                                if (firstCell) {
                                    firstCell.appendChild(indicator);
                                }
                            }
                        }
                    }
                }
                console.log("checker 3");
            }
            console.log("checker 2");

            // Show notification toast regardless of current page
            showMovementNotification(movement);
        
            // Update pagination links to include the new real-time additions count
            updatePaginationLinks();
        } catch (error) {
            console.error("Error in addMovementRow:", error);
        } finally {
            console.groupEnd();
        }
    }
    
    /**
     * Update pagination links with current realTimeAdditions count
     */
    function updatePaginationLinks() {
        // Only update links if we have pagination
        const paginationLinks = document.querySelectorAll('.pagination .page-link');
        if (paginationLinks.length === 0) return;
        
        paginationLinks.forEach(link => {
            const url = new URL(link.href);
            
            // Update or add realTimeAdditions parameter
            url.searchParams.set('realTimeAdditions', realTimeAdditions);
            
            // Update the href with new URL
            link.href = url.toString();
        });
        
        // If we're on page 1 and have real-time additions, update the total items display
        if (currentPage === 1) {
            const totalItemsDisplay = document.querySelector('.text-muted');
            if (totalItemsDisplay) {
                const displayedItems = Math.min((currentPage * pageSize) + realTimeAdditions, totalItems + realTimeAdditions);
                totalItemsDisplay.textContent = `Showing 1 to ${displayedItems} of ${totalItems + realTimeAdditions} entries`;
            }
        }
        
        console.log(`Updated pagination links with realTimeAdditions=${realTimeAdditions}`);
    }
    
    /**
     * Show a toast notification for new movement
     * @param {Object} movement - The movement data
     */
    function showMovementNotification(movement) {
        try {
            if (!movement) {
                console.error("Cannot show notification for null movement");
                return;
            }
            
            // Handle both camelCase and PascalCase property names
            const productName = movement.productName || movement.ProductName || "Unknown Product";
            const movementType = movement.movementType || movement.MovementType || "Unknown";
            const quantityBefore = movement.quantityBefore || movement.QuantityBefore || 0;
            const quantityAfter = movement.quantityAfter || movement.QuantityAfter || 0;
            const referenceId = movement.referenceId || movement.ReferenceId || "";
            
            const change = quantityAfter - quantityBefore;
            const isPositive = change >= 0;
            const changeText = isPositive ? `+${change}` : change;
            const toastClass = isPositive ? 'text-bg-success' : 'text-bg-danger';
            
            // Extract transaction information to group notifications
            let transactionInfo = '';
            const transactionMatch = referenceId.match(/SALE-(\d+)(?:-([a-zA-Z0-9]+))?/);
            if (transactionMatch && transactionMatch[2]) {
                // This is part of a transaction with an ID
                const saleId = transactionMatch[1];
                const transactionId = transactionMatch[2];
                
                // Check if we already have a toast for this transaction
                const existingToast = document.querySelector(`[data-transaction-id="${transactionId}"]`);
                if (existingToast) {
                    // Update existing toast instead of creating a new one
                    const productsList = existingToast.querySelector('.transaction-products');
                    const productsCount = existingToast.querySelector('.products-count');
                    
                    // Make sure both elements exist before proceeding
                    if (productsList && productsCount) {
                        const count = parseInt(productsCount.getAttribute('data-count') || '0') + 1;
                        
                        productsCount.setAttribute('data-count', count.toString());
                        productsCount.textContent = `${count} products`;
                        
                        // Add product to the list
                        const productItem = document.createElement('div');
                        productItem.className = 'd-flex justify-content-between my-1';
                        productItem.innerHTML = `
                            <span>${productName}</span>
                            <span class="fw-bold ${isPositive ? 'text-success' : 'text-danger'}">${changeText}</span>
                        `;
                        productsList.appendChild(productItem);
                        
                        // Refresh the toast timer
                        try {
                            const bsToast = bootstrap.Toast.getInstance(existingToast);
                            if (bsToast) {
                                bsToast.hide();
                                setTimeout(() => {
                                    bootstrap.Toast.getOrCreateInstance(existingToast).show();
                                }, 300);
                            }
                        } catch (error) {
                            console.error("Error refreshing toast:", error);
                        }
                        
                        return; // Don't create a new toast
                    }
                }
                
                transactionInfo = `data-transaction-id="${transactionId}"`;
            }
            
            const toastContainer = document.getElementById('movement-notifications');
            if (!toastContainer) {
                console.error("Toast container element not found");
                return;
            }
            
            const toastId = `toast-${Date.now()}`;
            
            const toastHtml = `
                <div id="${toastId}" class="toast ${toastClass}" role="alert" aria-live="assertive" aria-atomic="true" ${transactionInfo}>
                    <div class="toast-header">
                        <strong class="me-auto">${movementType}</strong>
                        <span class="products-count" data-count="1">1 product</span>
                        <small class="ms-2">Just now</small>
                        <button type="button" class="btn-close" data-bs-dismiss="toast" aria-label="Close"></button>
                    </div>
                    <div class="toast-body transaction-products">
                        <div class="d-flex justify-content-between">
                            <span>${productName}</span>
                            <span class="fw-bold">${changeText}</span>
                        </div>
                    </div>
                </div>
            `;
            
            toastContainer.insertAdjacentHTML('beforeend', toastHtml);
            const toastElement = document.getElementById(toastId);
            
            if (!toastElement) {
                console.error("Toast element not found after insertion");
                return;
            }
            
            try {
                const toast = new bootstrap.Toast(toastElement, { autohide: true, delay: 5000 });
                toast.show();
                
                // Remove toast from DOM after it's hidden
                toastElement.addEventListener('hidden.bs.toast', function () {
                    if (toastElement && toastElement.parentNode) {
                        toastElement.remove();
                    }
                });
            } catch (error) {
                console.error("Error showing toast:", error);
            }
        } catch (error) {
            console.error("Error in showMovementNotification:", error);
        }
    }
    
    /**
     * Show a consolidated notification for a multi-product transaction
     * @param {string} transactionId - The transaction ID
     * @param {Array} movements - Array of movements in the transaction
     */
    function showConsolidatedNotification(transactionId, movements) {
        if (!movements || movements.length === 0 || !transactionId) {
            console.log("Invalid parameters for consolidated notification");
            return;
        }
        
        const toastContainer = document.getElementById('movement-notifications');
        if (!toastContainer) {
            console.error("Toast container element not found");
            return;
        }
        
        const firstMovement = movements[0];
        if (!firstMovement) {
            console.error("First movement is null or undefined");
            return;
        }
        
        const movementType = firstMovement.movementType || firstMovement.MovementType || "Unknown";
        
        // Extract customer name from notes
        let customerName = "Customer";
        const notesText = firstMovement.notes || firstMovement.Notes || '';
        const customerMatch = notesText.match(/Sale to (.+?) \(/);
        if (customerMatch && customerMatch[1]) {
            customerName = customerMatch[1];
        }
        
        // Calculate total items and total change
        let totalItems = 0;
        let totalChange = 0;
        
        movements.forEach(m => {
            if (!m) return;
            const before = m.quantityBefore || m.QuantityBefore || 0;
            const after = m.quantityAfter || m.QuantityAfter || 0;
            const change = after - before;
            totalChange += change;
            // Estimate item count from change (this is an approximation)
            totalItems += Math.abs(change);
        });
        
        const isPositive = totalChange >= 0;
        const changeText = isPositive ? `+${totalChange}` : totalChange;
        const toastClass = isPositive ? 'text-bg-success' : 'text-bg-danger';
        
        const toastId = `toast-batch-${transactionId}`;
        
        // Remove existing toast for this transaction if it exists
        const existingToast = document.getElementById(toastId);
        if (existingToast && existingToast.parentNode) {
            existingToast.remove();
        }
        
        const toastHtml = `
            <div id="${toastId}" class="toast ${toastClass}" role="alert" aria-live="assertive" aria-atomic="true" data-transaction-id="${transactionId}">
                <div class="toast-header">
                    <strong class="me-auto">${movementType} - Multi-Product Transaction</strong>
                    <small class="ms-2">Just now</small>
                    <button type="button" class="btn-close" data-bs-dismiss="toast" aria-label="Close"></button>
                </div>
                <div class="toast-body">
                    <div class="mb-2">
                        <strong>${customerName}</strong>: ${movements.length} products, total change: 
                        <span class="fw-bold">${changeText}</span>
                    </div>
                    <div class="transaction-products small">
                        ${movements.map(m => {
                            const product = m.productName || m.ProductName;
                            const before = m.quantityBefore || m.QuantityBefore;
                            const after = m.quantityAfter || m.QuantityAfter;
                            const change = after - before;
                            const changePrefix = change >= 0 ? '+' : '';
                            const changeClass = change >= 0 ? 'text-success' : 'text-danger';
                            
                            return `<div class="d-flex justify-content-between">
                                <span>${product}</span>
                                <span class="${changeClass}">${changePrefix}${change}</span>
                            </div>`;
                        }).join('')}
                    </div>
                </div>
            </div>
        `;
        
        toastContainer.insertAdjacentHTML('beforeend', toastHtml);
        const toastElement = document.getElementById(toastId);
        
        if (!toastElement) {
            console.error("Toast element not found after insertion");
            return;
        }
        
        try {
            const toast = new bootstrap.Toast(toastElement, { autohide: true, delay: 8000 });
            toast.show();
            
            // Remove toast from DOM after it's hidden
            toastElement.addEventListener('hidden.bs.toast', function () {
                if (toastElement && toastElement.parentNode) {
                    toastElement.remove();
                }
            });
        } catch (error) {
            console.error("Error showing consolidated toast:", error);
        }
    }
    
    /**
     * Update summary numbers when new movements are added
     */
    function updateSummaryNumbers() {
        const rows = document.querySelectorAll('.movement-row:not([style*="display: none"])');
        
        // Update movement count
        const movementCount = document.getElementById('movementCount');
        if (movementCount) movementCount.textContent = rows.length;
        
        let totalInValue = 0;
        let totalOutValue = 0;
        
        // Calculate totals
        rows.forEach(row => {
            const changeElement = row.querySelector('.change-value');
            if (changeElement) {
                const changeText = changeElement.textContent.trim();
                const change = parseInt(changeText.replace(/[^0-9-+]/g, ''));
                
                if (change > 0) {
                    totalInValue += change;
                } else if (change < 0) {
                    totalOutValue += Math.abs(change);
                }
            }
        });
        
        // Update totals display
        const totalIn = document.getElementById('totalIn');
        const totalOut = document.getElementById('totalOut');
        const netChange = document.getElementById('netChange');
        
        if (totalIn) totalIn.textContent = totalInValue;
        if (totalOut) totalOut.textContent = totalOutValue;
        
        if (netChange) {
            const netValue = totalInValue - totalOutValue;
            netChange.textContent = netValue;
            
            if (netValue >= 0) {
                netChange.classList.remove('text-danger');
                netChange.classList.add('text-success');
            } else {
                netChange.classList.remove('text-success');
                netChange.classList.add('text-danger');
            }
        }
    }
});
