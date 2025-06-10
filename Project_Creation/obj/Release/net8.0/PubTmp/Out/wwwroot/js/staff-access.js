// staff-access.js - Handles real-time access level updates for staff members

$(document).ready(function() {
    console.log('staff-access.js loaded');
    
    // Check if body has the necessary classes
    console.log('Body classes:', $('body').attr('class'));
    console.log('Is authenticated:', $('body').hasClass('user-authenticated'));
    console.log('Is staff user:', $('body').hasClass('staff-user'));
    
    // Only run this code for staff users
    if (!$('body').hasClass('user-authenticated') || !$('body').hasClass('staff-user')) {
        console.log('Not a staff user or not authenticated, skipping staff access handling');
        return;
    }

    console.log('Initializing staff access level handling...');
    
    // Get the staff ID from the data attribute
    const staffId = $('body').data('staff-id');
    console.log('Staff ID from data attribute:', staffId);
    
    if (!staffId) {
        console.warn('Staff ID not found in body data attribute');
        return;
    }
    
    // Convert to string to match the server-side parameter type
    const staffIdString = staffId.toString();
    console.log('Staff ID found:', staffIdString);
    
    // Check if SignalR is available
    if (typeof signalR === 'undefined') {
        console.error('SignalR is not defined. Make sure signalR script is loaded before this script.');
        return;
    }
    
    // Set up SignalR connection
    const connection = new signalR.HubConnectionBuilder()
        .withUrl("/realtimehub")
        .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
        .configureLogging(signalR.LogLevel.Information)
        .build();
    
    // When connection starts, join the staff-specific group
    connection.start().then(function() {
        console.log('SignalR Connected for staff access updates');
        
        // Join staff-specific group for targeted updates
        console.log('Joining staff group:', 'staff_' + staffIdString);
        connection.invoke("JoinStaffGroup", staffIdString)
            .then(() => {
                console.log('Successfully joined staff group:', 'staff_' + staffIdString);
            })
            .catch(function(err) {
                console.error('Error joining staff group:', err.toString());
            });
    }).catch(function(err) {
        console.error('SignalR Connection Error:', err.toString());
    });
    
    // Helper function to extract values from special array format
    function extractValues(obj) {
        if (!obj) return [];
        if (Array.isArray(obj)) return obj;
        if (obj.$values && Array.isArray(obj.$values)) return obj.$values;
        return [];
    }
    
    // Listen for access level changes
    connection.on("AccessLevelChanged", function(targetStaffId, previous, added, removed) {
        console.log('Received AccessLevelChanged event for staff ID:', targetStaffId);
        console.log('Previous access levels (raw):', previous);
        console.log('Added access levels (raw):', added);
        console.log('Removed access levels (raw):', removed);
        
        // Extract values from the special format if needed
        const previousValues = extractValues(previous);
        const addedValues = extractValues(added);
        const removedValues = extractValues(removed);
        
        console.log('Extracted previous values:', previousValues);
        console.log('Extracted added values:', addedValues);
        console.log('Extracted removed values:', removedValues);
        
        // Only process if this notification is for the current user
        if (staffIdString === targetStaffId) {
            console.log('Access level changed for current user, handling update...');
            
            // Debug the content of the modal
            console.log('DEBUG - Will show modal with:');
            console.log('Previous values:', previousValues);
            console.log('Added values:', addedValues);
            console.log('Removed values:', removedValues);
            
            // Call the handler with the extracted values
            handleAccessLevelChange(previousValues, addedValues, removedValues);
        }
    });
    
    // Function to handle access level changes
    function handleAccessLevelChange(previous, added, removed) {
        console.log('Handling access level change...');
        console.log('Processing with previous:', previous);
        console.log('Processing with added:', added);
        console.log('Processing with removed:', removed);
        
        // First, refresh user claims immediately via AJAX
        refreshUserClaims().then(function(response) {
            console.log('Claims refreshed automatically before showing notification');
            
            // Create HTML content for the notification
            let htmlContent = '<div style="font-size: 1.1em; margin: 15px 0;">Your access permissions have been updated by the business owner.</div>';
            
            // Create a simpler list of access levels
            htmlContent += '<div style="margin-top: 15px; padding: 15px; background-color: #f8f9fa; border-radius: 4px; border: 1px solid #dee2e6;">';
            htmlContent += '<h5 style="margin-bottom: 15px; border-bottom: 1px solid #dee2e6; padding-bottom: 8px;">Your Access Levels</h5>';
            htmlContent += '<ul style="list-style-type: none; padding-left: 5px; font-size: 1.1em;">';
            
            // Get a list of all possible access levels
            const allAccessLevels = [
                "PublishedProducts", "Leads", "QuickSales", "Inventory", 
                "Campaigns", "Reports", "Notifications", "Calendar", "Chat"
            ];
            
            // First show current access levels (previous + added - removed)
            const currentAccessLevels = [];
            
            console.log('Building current access levels list...');
            
            // Add previous access levels that weren't removed
            if (previous && previous.length > 0) {
                console.log('Adding previous levels that were not removed');
                previous.forEach(level => {
                    const isInRemoved = removed && Array.isArray(removed) && 
                        removed.some(item => item === level);
                    
                    if (!isInRemoved) {
                        console.log(`Adding previous level: ${level}`);
                        currentAccessLevels.push(level);
                    } else {
                        console.log(`Skipping removed level: ${level}`);
                    }
                });
            }
            
            // Add new access levels
            if (added && added.length > 0) {
                console.log('Adding new levels');
                added.forEach(level => {
                    const isAlreadyInList = currentAccessLevels.some(item => item === level);
                    
                    if (!isAlreadyInList) {
                        console.log(`Adding new level: ${level}`);
                        currentAccessLevels.push(level);
                    } else {
                        console.log(`Level already in list, skipping: ${level}`);
                    }
                });
            }
            
            console.log('Current access levels:', currentAccessLevels);
            console.log('Added access levels:', added);
            console.log('Removed access levels:', removed);
            
            // Debug the HTML content before displaying access levels
            console.log('HTML content before displaying access levels:', htmlContent);
            console.log('DEBUG - Final list values for display:');
            console.log('Previous:', previous);
            console.log('Added:', added);
            console.log('Removed:', removed);
            console.log('Current access levels:', currentAccessLevels);
            
            // Display current access levels
            if (currentAccessLevels.length === 0) {
                console.log('No access levels to display in modal');
                htmlContent += `<li style="margin-bottom: 12px; color: #6c757d; font-style: italic;">
                    No access permissions assigned
                </li>`;
            } else {
                console.log('Displaying access levels in modal:', currentAccessLevels);
                currentAccessLevels.forEach(level => {
                    console.log(`Checking if ${level} is in added:`, added);
                    const isInAdded = added && Array.isArray(added) && added.some(item => item === level);
                    console.log(`Is ${level} in added array:`, isInAdded);
                    
                    if (isInAdded) {
                        // New access - green with (new) label
                        const newItemHtml = `<li style="margin-bottom: 12px; color: #28a745; font-weight: 500;">
                            ${level} <span style="color: #28a745; font-size: 0.9em; font-weight: bold;">(new)</span>
                        </li>`;
                        console.log(`Adding new item HTML for ${level}:`, newItemHtml);
                        htmlContent += newItemHtml;
                    } else {
                        // Unchanged access - normal text
                        const unchangedItemHtml = `<li style="margin-bottom: 12px; color: #212529;">
                            ${level}
                        </li>`;
                        console.log(`Adding unchanged item HTML for ${level}:`, unchangedItemHtml);
                        htmlContent += unchangedItemHtml;
                    }
                });
            }
            
            // Then show removed access levels
            if (removed && removed.length > 0) {
                removed.forEach(level => {
                    // Removed access - red with strikethrough and (removed) label
                    htmlContent += `<li style="margin-bottom: 12px; color: #dc3545; text-decoration: line-through;">
                        ${level} <span style="color: #dc3545; font-size: 0.9em; font-weight: bold;">(removed)</span>
                    </li>`;
                });
            }
            
            htmlContent += '</ul>';
            htmlContent += '</div>';
            
            // Log the list HTML content for debugging
            console.log('HTML content for access levels list:', htmlContent);
            
            // Add notification that changes have already been applied
            htmlContent += '<div style="background: #e8f4fd; padding: 10px; border-radius: 5px; margin-top: 15px; text-align: center;">';
            htmlContent += '<i class="fas fa-info-circle me-2"></i> <b>Your access permissions have already been updated!</b><br>';
            htmlContent += 'Changes have been applied automatically. Click Reload to refresh the page.';
            htmlContent += '</div>';
            
            // Check if SweetAlert is available
            if (typeof Swal === 'undefined') {
                console.error('SweetAlert is not defined. Make sure SweetAlert script is loaded.');
                return;
            }
            
            // Log the complete HTML content before showing the modal
            console.log('Complete HTML content for modal:', htmlContent);
            
            // Show the SweetAlert with the detailed information
            console.log('About to show SweetAlert modal...');
            
            Swal.fire({
                title: '<strong>Access Levels Changed</strong>',
                html: htmlContent,
                icon: 'info',
                confirmButtonText: 'Reload',
                confirmButtonColor: '#3085d6',
                width: '600px',
                didOpen: () => {
                    console.log('SweetAlert modal opened successfully');
                }
            }).then((result) => {
                if (result.isConfirmed) {
                    // Reload the page when OK is clicked
                    window.location.reload();
                }
            });
            
            // Also update UI elements based on new access levels
            console.log('Updating UI elements with:', added, removed);
            applyAccessLevelChanges(added, removed);
        }).catch(function(error) {
            console.error('Failed to refresh claims automatically:', error);
            
            // Create HTML content for the notification with error
            let htmlContent = '<div style="font-size: 1.1em; margin: 15px 0;">Your access permissions have been updated by the business owner.</div>';
            
            // Create a simpler list of access levels
            htmlContent += '<div style="margin-top: 15px; padding: 15px; background-color: #f8f9fa; border-radius: 4px; border: 1px solid #dee2e6;">';
            htmlContent += '<h5 style="margin-bottom: 15px; border-bottom: 1px solid #dee2e6; padding-bottom: 8px;">Your Access Levels</h5>';
            htmlContent += '<ul style="list-style-type: none; padding-left: 5px; font-size: 1.1em;">';
            
            // Get a list of all possible access levels
            const allAccessLevels = [
                "PublishedProducts", "Leads", "QuickSales", "Inventory", 
                "Campaigns", "Reports", "Notifications", "Calendar", "Chat"
            ];
            
            // First show current access levels (previous + added - removed)
            const currentAccessLevels = [];
            
            console.log('Building current access levels list for modal...');
            
            // Add previous access levels that weren't removed
            if (previous && previous.length > 0) {
                console.log('Adding previous levels that were not removed');
                previous.forEach(level => {
                    const isInRemoved = removed && Array.isArray(removed) && 
                        removed.some(item => item === level);
                    
                    if (!isInRemoved) {
                        console.log(`Adding previous level to display: ${level}`);
                        currentAccessLevels.push(level);
                    } else {
                        console.log(`Skipping removed level: ${level}`);
                    }
                });
            }
            
            // Add new access levels
            if (added && added.length > 0) {
                console.log('Adding new levels to display');
                added.forEach(level => {
                    const isAlreadyInList = currentAccessLevels.some(item => item === level);
                    
                    if (!isAlreadyInList) {
                        console.log(`Adding new level to display: ${level}`);
                        currentAccessLevels.push(level);
                    } else {
                        console.log(`Level already in list, skipping: ${level}`);
                    }
                });
            }
            
            console.log('Final current access levels for display:', currentAccessLevels);
            
            // Display current access levels
            if (currentAccessLevels.length === 0) {
                console.log('No access levels to display in error modal');
                htmlContent += `<li style="margin-bottom: 12px; color: #6c757d; font-style: italic;">
                    No access permissions assigned
                </li>`;
            } else {
                console.log('Displaying access levels in error modal:', currentAccessLevels);
                currentAccessLevels.forEach(level => {
                    console.log(`Checking if ${level} is in added (error modal):`, added);
                    const isInAdded = added && Array.isArray(added) && added.some(item => item === level);
                    console.log(`Is ${level} in added array (error modal):`, isInAdded);
                    
                    if (isInAdded) {
                        // New access - green with (new) label
                        htmlContent += `<li style="margin-bottom: 12px; color: #28a745; font-weight: 500;">
                            ${level} <span style="color: #28a745; font-size: 0.9em; font-weight: bold;">(new)</span>
                        </li>`;
                    } else {
                        // Unchanged access - normal text
                        htmlContent += `<li style="margin-bottom: 12px; color: #212529;">
                            ${level}
                        </li>`;
                    }
                });
            }
            
            // Then show removed access levels
            if (removed && removed.length > 0) {
                removed.forEach(level => {
                    // Removed access - red with strikethrough and (removed) label
                    htmlContent += `<li style="margin-bottom: 12px; color: #dc3545; text-decoration: line-through;">
                        ${level} <span style="color: #dc3545; font-size: 0.9em; font-weight: bold;">(removed)</span>
                    </li>`;
                });
            }
            
            htmlContent += '</ul>';
            htmlContent += '</div>';
            
            // Add error message
            htmlContent += '<div style="background: #fff3cd; padding: 10px; border-radius: 5px; margin-top: 15px; text-align: center; border-left: 4px solid #ffc107;">';
            htmlContent += '<i class="fas fa-exclamation-triangle me-2"></i> <b>There was an issue applying your changes automatically.</b><br>';
            htmlContent += 'Click "Apply Changes" to refresh the page and apply all changes.';
            htmlContent += '</div>';
            
            // Show the SweetAlert with the detailed information and refresh button
            Swal.fire({
                title: '<strong>Access Levels Changed</strong>',
                html: htmlContent,
                icon: 'warning',
                confirmButtonText: 'Apply Changes',
                confirmButtonColor: '#3085d6',
                width: '600px'
            }).then((result) => {
                if (result.isConfirmed) {
                    // Refresh the page to apply the new permissions
                    location.reload();
                }
            });
            
            // Try to update UI elements based on new access levels
            applyAccessLevelChanges(added, removed);
        });
    }
    
    // Function to apply access level changes to the UI without page reload
    function applyAccessLevelChanges(added, removed) {
        console.log('Applying access level changes to UI...');
        console.log('Added access levels for UI update:', added);
        console.log('Removed access levels for UI update:', removed);
        
        // Update navigation items visibility based on access level changes
        if (removed && removed.length > 0) {
            console.log('Processing removed access levels for UI');
            removed.forEach(function(level) {
                // Hide nav items that require this access level
                $(`[data-access-level="${level}"]`).addClass('d-none');
                console.log(`Hiding elements with access level: ${level}`);
            });
        } else {
            console.log('No removed access levels to process for UI');
        }
        
        if (added && added.length > 0) {
            console.log('Processing added access levels for UI');
            added.forEach(function(level) {
                // Show nav items that now have access
                $(`[data-access-level="${level}"]`).removeClass('d-none');
                console.log(`Showing elements with access level: ${level}`);
            });
        } else {
            console.log('No added access levels to process for UI');
        }
    }
    
    // Function to refresh user claims via AJAX - returns a Promise
    function refreshUserClaims() {
        console.log('Refreshing user claims...');
        
        return new Promise((resolve, reject) => {
            // Get the anti-forgery token
            const token = $('input[name="__RequestVerificationToken"]').val();
            console.log('Anti-forgery token found:', !!token);
            
            $.ajax({
                url: '/Dashboard/RefreshClaims/',
                type: 'POST',
                headers: token ? {
                    'RequestVerificationToken': token
                } : {},
                success: function(response) {
                    console.log('Claims refreshed successfully:', response);
                    
                    if (response.success) {
                        // Update any UI elements that depend on claims
                        console.log('New access level:', response.accessLevel);
                        
                        // Show a toast notification that claims were updated
                        const toastId = `toast-${Date.now()}`;
                        const toast = `
                            <div id="${toastId}" class="toast align-items-center text-white bg-success border-0" role="alert" aria-live="assertive" aria-atomic="true" data-bs-delay="3000" data-bs-autohide="true">
                                <div class="d-flex">
                                    <div class="toast-body">
                                        <i class="fas fa-check-circle me-2"></i> Your access permissions have been updated.
                                    </div>
                                    <div class="me-2 m-auto" style="cursor: pointer; font-size: 1.25rem;" onclick="document.getElementById('${toastId}').remove()">×</div>
                                </div>
                            </div>`;
                        $('.toast-container').append(toast);
                        
                        // Check if Bootstrap is available
                        if (typeof bootstrap !== 'undefined') {
                            // Initialize and show the toast using Bootstrap's JS API
                            const toastElement = document.getElementById(toastId);
                            if (toastElement) {
                                const bootstrapToast = new bootstrap.Toast(toastElement);
                                bootstrapToast.show();
                            }
                        }
                        
                        // Resolve the promise with the response
                        resolve(response);
                    } else {
                        // If the server returned success:false, reject with the response
                        reject(response);
                    }
                },
                error: function(xhr) {
                    console.error('Failed to refresh claims:', xhr);
                    reject(xhr);
                }
            });
        });
    }
}); 