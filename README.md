# TomasAI Investment FundManager (IFM)

## Overview

This project, TomasAI Investment Fund Manager (IFM), is a comprehensive system designed for advanced investment fund management. It appears to specialize in options trading and pricing, incorporating sophisticated features such as Monte Carlo simulations (potentially CUDA-accelerated for high performance) and predictive modeling capabilities to support investment decisions.

## Key Modules/Components

The IFM system is architected in a modular fashion, comprising several key project groups:

*   **Domain**: Contains the core business logic, entities, and rules that define the investment fund management domain.
*   **Application**: Orchestrates domain logic, handles application-level events, and exposes services. This layer includes server-side components and client APIs for various functionalities.
*   **Services**: Provides specific, often independently hostable, functionalities such as market data analysis, order execution, trade planning, option pricing, and more.
*   **Framework**: Consists of reusable, cross-cutting components for common concerns like data storage, messaging (utilizing Kafka, REST, SignalR), caching (with Redis integration), and other foundational capabilities.
*   **Shared**: A collection of common libraries, data models, and utility functions used across multiple projects within the IFM ecosystem.
*   **UI**: Includes various client applications for user interaction:
    *   Desktop Clients: `TomasAI.IFM.Application.ServerManager` (WPF/Windows Forms) for system management and `TomasAI.IFM.UI.Net` (Windows Forms) for user operations.
    *   Web Client: `TomasAI.IFM.UI.Angular` suggests the presence of a web-based interface.
*   **MonteCarloOptionPricer**: Specialized libraries dedicated to option pricing, featuring CUDA support for accelerating Monte Carlo simulations.
*   **Database**: The system relies on extensive database support, primarily SQL Server (as indicated by `.sqlproj` files), with potential integration of other database technologies like PostgreSQL and ScyllaDB for specific needs (inferred from project naming conventions).

## Key Technologies

The IFM platform leverages a diverse set of technologies to deliver its functionalities:

*   **Core Development**: .NET (primarily C#)
*   **High-Performance Computing**: NVIDIA CUDA (for Monte Carlo simulations)
*   **Real-time Communication**: SignalR
*   **Messaging/Event Streaming**: Apache Kafka
*   **Caching**: Redis
*   **Web UI**: Angular
*   **Database**: Microsoft SQL Server, with potential use of PostgreSQL and ScyllaDB for specific data storage requirements.

## Architecture

The TomasAI Investment Fund Manager follows a modern, distributed architecture:

*   **Service-Oriented**: Functionality is broken down into distinct services that can be developed, deployed, and scaled independently.
*   **Layered Design**: The system exhibits a clear separation of concerns with distinct layers for UI, Application Logic, Domain Model, and Infrastructure (Framework).
*   **Event-Driven**: The use of Apache Kafka and numerous projects with "Event" and "EventConsumer" in their names indicates a strong reliance on event-driven patterns for inter-service communication and data propagation.

## Getting Started

Given the scale and complexity of the TomasAI IFM system, a comprehensive setup requires careful configuration of its multiple server-side and client-side components.

*   **Main Solution**: The primary solution file appears to be `TomasAI.InvestmentFundManager.sln`, which orchestrates the numerous projects.
*   **General Setup Steps**:
    1.  **Database Setup**: Initialize and configure the required databases. Microsoft SQL Server is the primary database, but other systems like PostgreSQL or ScyllaDB might be needed for specific modules.
    2.  **Server Applications**: Configure and launch the various server applications. Key applications include `TomasAI.IFM.Application.ServerManager` and potentially other service hosts found in `TomasAI.IFM.Application.*.Server` or `TomasAI.IFM.Service.*.HostedService` project directories.
    3.  **Client Applications**: Run one of the client applications to interact with the system. Options include:
        *   `TomasAI.IFM.UI.Net` (Windows Forms desktop application)
        *   `TomasAI.IFM.UI.Angular` (Web-based client - requires separate build and deployment of the Angular frontend)
        *   `TomasAI.IFM.Application.ServerManager` (WPF/Windows Forms, may also serve client functions or provide management interfaces).

*   **Note**: This overview provides general guidance. Detailed setup and deployment instructions for each component would typically be found within their respective project directories or dedicated deployment documentation (if available).

## Disclaimer

This README provides a high-level overview of the TomasAI Investment Fund Manager (IFM) system based on an automated analysis of its codebase structure. For a deeper understanding, contribution, or deployment, it is highly recommended to consult any detailed documentation that may exist within specific project folders or in dedicated internal documentation repositories.
